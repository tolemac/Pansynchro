using System;

namespace Pansynchro.Core.CustomTypes
{
	public readonly record struct CanonicalGeo(string Wkt, int? Srid)
	{
		public static int Wgs84Srid => 4326;

		public CanonicalGeo WithDefaultSrid(int defaultSrid) => Srid.HasValue ? this : this with { Srid = defaultSrid };

		public string ToText(int? defaultSrid = null)
		{
			var srid = Srid ?? defaultSrid;
			return srid.HasValue ? $"SRID={srid.Value};{Wkt}" : Wkt;
		}

		public override string ToString() => ToText(null);

		public static CanonicalGeo Parse(string? text, int? defaultSrid = null)
		{
			var value = text ?? string.Empty;
			if (value.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase)) {
				var split = value.Split(';', 2);
				if (split.Length == 2 && int.TryParse(split[0][5..], out var srid)) {
					return new CanonicalGeo(split[1], srid);
				}
			}

			return new CanonicalGeo(value, defaultSrid);
		}
	}
}
