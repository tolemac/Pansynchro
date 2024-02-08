﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Pansynchro.Core.Connectors;
using Pansynchro.PanSQL.Compiler.Ast;
using Pansynchro.PanSQL.Compiler.DataModels;
using Pansynchro.PanSQL.Compiler.Helpers;
using Pansynchro.PanSQL.Core;

namespace Pansynchro.PanSQL.Compiler.Steps
{
	using static System.Net.WebRequestMethods;
	using StringLiteralExpression = Ast.StringLiteralExpression;

	internal class Codegen : VisitorCompileStep
	{
		public Script Output { get; } = new();
		
		private bool _transformer;
		private readonly HashSet<string> _connectorRefs = [];
		private readonly HashSet<string> _connectors = [];
		private readonly HashSet<string> _sources = [];
		private readonly List<ScriptVarDeclarationStatement> _scriptVars = [];
		private readonly List<DataFieldModel> _initFields = [];
		private readonly List<ImportModel> _imports = [];

		private static readonly ImportModel[] USING_BLOCK = [
			"System",
			"System.Collections.Generic",
			"System.Data",
			"System.Threading.Tasks",
			"Pansynchro.Core",
			"Pansynchro.Core.DataDict",
			"Pansynchro.Core.Connectors",
			"Pansynchro.PanSQL.Core",
			new ("Pansynchro.PanSQL.Core.Credentials", true)];

		private static readonly ImportModel[] USING_DB = [
			"NMemory",
			"NMemory.Tables",
			"NMemory.Indexes"];

		private const string CSPROJ = @"<Project Sdk=""Microsoft.NET.Sdk"">

	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net8.0</TargetFramework>
		<Nullable>enable</Nullable>
		<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
		<NoWarn>$(NoWarn);CS8621</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include=""NMemory"" Version=""*"" />
		<PackageReference Include=""Pansynchro.Core"" Version=""*"" />
		<PackageReference Include=""Pansynchro.PanSQL.Core"" Version=""*"" />
		{0}
	</ItemGroup>

	<ItemGroup>
		<None Update=""connectors.pansync"">
			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
		</None>
	</ItemGroup>
	
</Project>
";
		public override void Execute(PanSqlFile f)
		{
			base.Execute(f);
			var connectorRefs = string.Join(Environment.NewLine + "\t\t", _connectorRefs.Select(BuildConnectorReference));
			Output.ProjectFile = string.Format(CSPROJ, connectorRefs);
			Output.Connectors =
				ConnectorRegistry.WriteConnectors([.. _connectors.Order()], _sources.Count > 0 ? [.. _sources.Order()] : null);
		}

		private string BuildConnectorReference(string source)
		{
			var asm = ConnectorRegistry.GetLocation(source);
			return $"<PackageReference Include=\"{asm}\" Version=\"*\" />";
		}

		private List<CSharpStatement> _mainBody = null!;

		public override void OnFile(PanSqlFile node)
		{
			_imports.AddRange(USING_BLOCK);
			if (node.Database.Count > 0) {
				_imports.AddRange(USING_DB);
			}
			_transformer = _file.Mappings.Count != 0 || _file.NsMappings.Count != 0;
			var classes = new List<ClassModel>();
			if (_transformer) {
				classes.Add(BuildTransformer(node, _imports));
			}
			_mainBody = [];
			base.OnFile(node);
			if (_scriptVars.Count > 0) {
				WriteScriptVarInit();
			}
			var args = _scriptVars.Count > 0 ? "string[] args" : null;
			var main = new Method("public static async", "Main", "Task", args, _mainBody);
			classes.Add(new("static", "Program", null, null, [], [main]));
			var result = new FileModel(SortImports().ToArray(), [.. classes]);
			Output.SetFile(result);
		}

		private void WriteScriptVarInit()
		{
			for (int i = _scriptVars.Count - 1; i >= 0; --i) {
				var sVar = _scriptVars[i];
				var decl = $"{TypesHelper.FieldTypeToCSharpType(sVar.FieldType)} {sVar.ScriptName}";
				if (sVar.Expr != null) {
					decl = $"{decl} = {sVar.Expr}";
				}
				_mainBody.Insert(0, new ExpressionStatement(new CSharpStringExpression(decl)));
			}
			var required = _scriptVars.Any(sv => sv.Expr == null);
			var initializer = $"new VariableReader(args, {required.ToString().ToLowerInvariant()})";
			foreach (var sVar in _scriptVars) {
				var methodName = sVar.Expr != null ? "TryReadVar" : "ReadVar";
				var passType = sVar.Expr != null ? "ref" : "out";
				initializer = $"{initializer}.{methodName}({sVar.Name.Name.ToLiteral()}, {passType} {sVar.ScriptName})";
			}
			_mainBody.Insert(_scriptVars.Count, new CSharpStringExpression($"var __varResult = {initializer}.Result"));
			var checkBody = new Block([new CSharpStringExpression("System.Console.WriteLine(__varResult)"), new ReturnStatement()]);
			_mainBody.Insert(_scriptVars.Count + 1, new IfStatement(new BooleanExpression(BoolExpressionType.NotEquals, new ReferenceExpression("__varResult"), new CSharpStringExpression("null")), checkBody));
		}

		private IEnumerable<ImportModel?> SortImports()
		{
			var groups = _imports.DistinctBy(i => i.Name).ToLookup(i => i.Name.Split('.')[0]);
			foreach (var group in groups) {
				foreach (var item in group.OrderBy(i => i.Name)) {
					yield return item;
				}
				yield return null;
			}
		}

		private ClassModel BuildTransformer(PanSqlFile node, List<ImportModel> imports)
		{
			var fields = new List<DataFieldModel>();
			var methods = new List<Method>();
			methods.AddRange(_file.Lines.OfType<VarDeclaration>().Select(GetVarScript).Where(m => m != null)!);
			methods.AddRange(_file.Lines.OfType<SqlTransformStatement>().SelectMany(t => GetSqlScript(t, imports)));
			_initFields.AddRange(_file.Lines.OfType<SqlTransformStatement>().SelectMany(t => GetInitVars(t)));
			if (_file.Producers.Count > 0) {
				methods.Add(BuildStreamLast());
			}
			var body = new List<CSharpStatement>();
			foreach (var field in _initFields) {
				body.Add(new CSharpStringExpression($"{field.Name} = {field.Name[1..]}"));
			}
			foreach (var tf in _file.Transformers) {
				var tableName = VariableHelper.GetStream(_file.Vars[tf.Key.Name]).Name.ToString();
				body.Add(new CSharpStringExpression($"_streamDict.Add({tableName.ToLiteral()}, {tf.Value})"));
			}
			foreach (var pr in _file.Producers) {
				var tableName = VariableHelper.GetStream(_file.Vars[pr.Key.Name]).Name.ToString();
				body.Add(new CSharpStringExpression($"_producers.Add((destDict.GetStream({tableName.ToLiteral()}), {pr.Value}))"));
			}
			foreach (var m in _file.Mappings) {
				if (m.Key != m.Value) {
					body.Add(new CSharpStringExpression($"_nameMap.Add(StreamDescription.Parse({m.Key.ToLiteral()}), StreamDescription.Parse({m.Value.ToLiteral()}))"));
				}
			}
			foreach (var nm in _file.NsMappings) {
				if (nm.Key != nm.Value) {
					var l = nm.Key == null ? "null" : nm.Key.ToLiteral();
					var r = nm.Value == null ? "null" : nm.Value.ToLiteral();
					body.Add(new CSharpStringExpression($"MapNamespaces({l}, {r})"));
				}
			}
			var args = string.Join(", ", ["DataDictionary destDict", .. _initFields.Select(f => $"{f.Type} {f.Name[1..]}")]);
			methods.Add(new Method("public", "Sync", "", args, body, true, "destDict"));
			var subclasses = node.Database.Count > 0 ? new ClassModel[] { GenerateDatabase(node.Database, fields) } : null;
			return new ClassModel("", "Sync", "StreamTransformerBase", subclasses, [.. fields.Concat(_initFields)], [.. methods]);
		}

		private static Method BuildStreamLast()
		{
			var body = new List<CSharpStatement>();
			var loopBody = new List<CSharpStatement>() {
				new YieldReturn(new CSharpStringExpression("new DataStream(stream.Name, StreamSettings.None, new ProducingReader(producer(), stream))"))
			};
			body.Add(new ForeachLoop("(stream, producer)", "_producers", loopBody));
			body.Add(new CSharpStringExpression("await Task.CompletedTask"));
			return new Method("public override", "StreamLast", "async IAsyncEnumerable<DataStream>", null, body);
		}

		public override void OnAnalyzeStatement(AnalyzeStatement node)
		{
			string line;
			var list = node.IncludeList;
			if (list != null) {
				line = $"await ((IQueryableSchemaAnalyzer){node.Conn}).AnalyzeAsync(\"{node.Dict}\", [{string.Join(", ", list.Select(l => l.ToString()!.ToLiteral()))}])";
			} else {
				list = node.ExcludeList;
				if (list != null) {
					line = $"await ((IQueryableSchemaAnalyzer){node.Conn}).AnalyzeExcludingAsync(\"{node.Dict}\", [{string.Join(", ", list.Select(l => l.ToString()!.ToLiteral()))}])";
				} else {
					line = $"await {node.Conn}.AnalyzeAsync(\"{node.Dict}\")";
				}
			}
			_mainBody.Add(new VarDecl(node.Dict.Name, new CSharpStringExpression(line)));
			if (node.Optimize) {
				_mainBody.Add(new CSharpStringExpression($"{node.Dict} = await {node.Conn}.Optimize({node.Dict}, _ => {{ }})"));
			}
		}

		private ClassModel GenerateDatabase(List<DataClassModel> database, List<DataFieldModel> outerFields)
		{
			var fields = new List<DataFieldModel>();
			var subclasses = database.Select(m => GenerateModelClass(m, fields)).ToArray();
			var ctor = GenerateDatabaseConstructor(database);
			outerFields.Add(new("__db", "readonly DB", "new()"));
			if (_file.Producers.Count > 0) {
				outerFields.Add(new("_producers", "readonly List<(StreamDefinition stream, Func<IEnumerable<object?[]>> producer)>", "new()"));
			}
			return new ClassModel("private", "DB", "Database", subclasses, [.. fields], [ctor]);
		}

		private static Method GenerateDatabaseConstructor(List<DataClassModel> database)
		{
			var body = new List<CSharpStatement> { new CSharpStringExpression("NMemory.NMemoryManager.DisableObjectCloning = true") };
			foreach (var model in database) {
				GenerateModelConstruction(model, body);
			}
			return new Method("public", "DB", "", null, body, true);
		}

		private static void GenerateModelConstruction(DataClassModel model, List<CSharpStatement> body)
		{
			body.Add(new CSharpStringExpression($"{model.Name[..^1]} = Tables.Create<{model.Name}, {TypesHelper.ModelIdentityType(model)}>(t => {TypesHelper.ModelIdentityFields(model, "t")})"));
			body.Add(new CSharpStringExpression($"{model.Name}{TypesHelper.ModelIdentityName(model)} = (IUniqueIndex<{model.Name}, {TypesHelper.ModelIdentityType(model)}>){model.Name[..^1]}.PrimaryKeyIndex"));
		}

		private static ClassModel GenerateModelClass(DataClassModel model, List<DataFieldModel> outerFields)
		{
			var fields = model.Fields.Select(f => new DataFieldModel(f.Name, f.Type, null, true)).ToArray();
			var lines = new List<CSharpStatement>();
			for (int i = 0; i < model.Fields.Length; ++i) {
				var field = model.Fields[i];
				lines.Add(new CSharpStringExpression($"this.{field.Name} = {string.Format(field.Initializer!, i)}"));
			}
			var ctorArgs = model.FieldConstructor ? BuildFieldConstructorArgs(fields) : "IDataReader r";
			var ctor = new Method("public", model.Name, "", ctorArgs, lines, true);
			outerFields.Add(new(model.Name[..^1], $"ITable<{model.Name}>", null, true));
			outerFields.Add(new($"{model.Name}{TypesHelper.ModelIdentityName(model)}", $"IUniqueIndex<{model.Name}, {TypesHelper.ModelIdentityType(model)}>", null, true));
			return new ClassModel("public", model.Name, null, null, fields, [ctor]);
		}

		private static string BuildFieldConstructorArgs(DataFieldModel[] fields) => string.Join(", ", fields.Select(f => $"{f.Type} {f.Name.ToLower()}_"));

		public override void OnLoadStatement(LoadStatement node)
		{
			var cs = node.Dict.ToString().ToCompressedString();
			_mainBody.Add(new VarDecl(node.Name, new CSharpStringExpression($"DataDictionaryWriter.Parse(CompressionHelper.Decompress({cs.ToLiteral()}))")));
		}

		public override void OnSaveStatement(SaveStatement node)
		{
			_mainBody.Add(new CSharpStringExpression($"{node.Name}.SaveToFile(FilenameHelper.Normalize({node.Filename.ToLiteral()}))"));
		}

		public override void OnVarDeclaration(VarDeclaration node)
		{ }

		public override void OnOpenStatement(OpenStatement node)
		{
			var method = node.Type switch {
				OpenType.Read => "GetReader",
				OpenType.Write => "GetWriter",
				OpenType.Analyze => "GetAnalyzer",
				OpenType.Source => "GetSource",
				OpenType.Sink => "GetSink",
				_ => throw new NotImplementedException(),
			};
			if (node.Connector.Equals("Network", StringComparison.InvariantCultureIgnoreCase)) {
				ProcessNetworkConnection(node);
			}
			Visit(node.Creds);
			var line = $"ConnectorRegistry.{method}({node.Connector.ToLiteral()}, {node.Creds})";
			_mainBody.Add(new VarDecl(node.Name, new CSharpStringExpression(line)));
			if (node.Source != null) {
				line = node.Type switch {
					OpenType.Read or OpenType.Analyze => $"((ISourcedConnector){node.Name}).SetDataSource({node.Source})",
					OpenType.Write => $"((ISinkConnector){node.Name}).SetDataSink({node.Source})",
					_ => throw new NotImplementedException()
				};
				_mainBody.Add(new CSharpStringExpression(line));
			}
			_connectorRefs.Add(node.Connector);
			(node.Type is OpenType.Read or OpenType.Write or OpenType.Analyze ? _connectors : _sources).Add(node.Connector);
		}

		private void ProcessNetworkConnection(OpenStatement node)
		{
			if (node.Creds.Method == "__direct") {
				var dictRef = node.Dictionary!.Name;
				var filename = CodeBuilder.NewNameReference("filename");
				_mainBody.Add(new VarDecl(filename.Name, new CSharpStringExpression("System.IO.Path.GetTempFileName()")));
				_mainBody.Add(new CSharpStringExpression($"{dictRef}.SaveToFile({filename})"));
				var oldValue = node.Creds.Value is StringLiteralExpression sl ? sl.Value : node.Creds.Value.ToString();
				node.Creds = new CredentialExpression("__literal", new StringLiteralExpression($"\"{oldValue};\" + {filename}")); 
			}
		}

		private IEnumerable<DataFieldModel> GetInitVars(SqlTransformStatement node)
		{
			var vars = node.DataModel.Model.ScriptVariables;
			foreach (var sVar in vars) {
				var decl = ((ScriptVarDeclarationStatement)_file.Vars[sVar[1..]].Declaration);
				yield return new DataFieldModel('_' + decl.Name.Name, TypesHelper.FieldTypeToCSharpType(decl.FieldType), null, IsReadonly: true);
			}
		}

		private IEnumerable<Method> GetSqlScript(SqlTransformStatement node, List<ImportModel> imports)
		{
			var ctes = new Dictionary<string, string>();
			foreach (var cte in node.Ctes) {
				var script = cte.Model.GetScript(CodeBuilder, node.Indices, imports, ctes);
				yield return script;
				ctes[cte.Name] = script.Name;
			}
			var method = node.DataModel.GetScript(CodeBuilder, node.Indices, imports, ctes);
			if (_file.Transformers.ContainsKey(node.Tables[0])) {
				_file.Producers.Add(node.Tables[0], method.Name);
			} else {
				var table = VariableHelper.GetInputStream(node, _file);
				if (table != null) {
					_file.Transformers.Add(table, method.Name);
				}
			}
			yield return method;
		}

		private Method? GetVarScript(VarDeclaration decl)
		{
			if (decl.Type == VarDeclarationType.Table && decl.Stream != null) {
				var methodName = CodeBuilder.NewNameReference("Transformer");
				_file.Transformers.Add(_file.Vars[decl.Name], methodName.Name);
				var tableName = decl.Stream.Name;
				var body = new List<CSharpStatement>();
				var loopBody = new List<CSharpStatement>() { new CSharpStringExpression($"__db.{tableName}.Insert(new DB.{tableName}_(r))") };
				body.Add(new WhileLoop(new CSharpStringExpression("r.Read()"), loopBody));
				body.Add(new YieldBreak());
				return new Method("private", methodName.Name, "IEnumerable<object?[]>", "IDataReader r", body);
			}
			return null;
		}

		public override void OnSqlStatement(SqlTransformStatement node)
		{ }

		public override void OnMapStatement(MapStatement node)
		{ }

		public override void OnSyncStatement(SyncStatement node)
		{
			var input = _file.Vars[node.Input.Name];
			var inputDict = ((OpenStatement)input.Declaration).Dictionary;
			var inputName = CodeBuilder.NewNameReference("reader");
			var output = _file.Vars[node.Output.Name];
			var outputDict = ((OpenStatement)output.Declaration).Dictionary!.Name;
			_mainBody.Add(new VarDecl(inputName.Name, new CSharpStringExpression($"{input.Name}.ReadFrom({inputDict})")));
			if (_transformer) {
				var args = string.Join(", ", [outputDict, .. _initFields.Select(f => ((ScriptVarDeclarationStatement)_file.Vars[f.Name[1..]].Declaration).ScriptName)]);
				_mainBody.Add(new CSharpStringExpression($"{inputName} = new Sync({args}).Transform({inputName})"));
			}
			_mainBody.Add(new CSharpStringExpression($"await {output.Name}.Sync({inputName}, {outputDict})"));
		}

		public override void OnFunctionCallExpression(FunctionCallExpression node)
		{
			base.OnFunctionCallExpression(node);
			_imports.Add(node.Namespace!);
		}

		public override void OnScriptVarDeclarationStatement(ScriptVarDeclarationStatement node)
		{
			_scriptVars.Add(node);
		}

		public override void OnJsonInterpolatedExpression(JsonInterpolatedExpression node)
		{
			_imports.Add("System.Text.Json.Nodes");
			var name = CodeBuilder.NewNameReference("json");
			_mainBody.Add(new VarDecl(name.Name, new CSharpStringExpression($"JsonNode.Parse({node.JsonString})!")));
			foreach (var interp in node.Ints) {
				_mainBody.Add(new CSharpStringExpression(interp.Key.ToCodeString(name.Name, interp.Value)));
			}
			node.VarName = name.Name;
		}

		public override IEnumerable<(Type, Func<CompileStep>)> Dependencies()
			=> [Dependency<TypeCheck>(), Dependency<BuildMappings>(), Dependency<BuildDatabase>()];
	}
}
