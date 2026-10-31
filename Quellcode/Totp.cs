// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using KeePassLib;
using KeePassLib.Security;

namespace FluentTheme
{
	/// <summary>Zeitbasierte Einmalcodes (TOTP, RFC 6238) für die Detailansicht.
	/// Liest die Felder, die KeePass selbst anlegt (TimeOtp-Secret-Base32 usw.), sowie
	/// ein Feld „otp“ im Format von KeePassXC (otpauth://…) oder KeeOtp (key=…&amp;step=…).</summary>
	internal sealed class Totp
	{
		public byte[] Key;
		public int Digits = 6;
		public int Period = 30;
		public string Algorithm = "SHA1";

		/// <summary>Felder, aus denen das Geheimnis stammt (werden unter „Weitere Felder“ ausgeblendet).</summary>
		public readonly List<string> SourceFields = new List<string>();

		public static Totp FromEntry(PwEntry pe)
		{
			if(pe == null) return null;
			try
			{
				Totp t = FromKeePass(pe);
				if(t == null) t = FromOtpField(pe);
				if((t == null) || (t.Key == null) || (t.Key.Length == 0)) return null;
				if((t.Digits < 6) || (t.Digits > 10)) t.Digits = 6;
				if((t.Period < 1) || (t.Period > 3600)) t.Period = 30;
				return t;
			}
			catch(Exception) { return null; }
		}

		private static string Read(PwEntry pe, string key)
		{
			ProtectedString ps = pe.Strings.Get(key);
			return ((ps != null) ? ps.ReadString().Trim() : null);
		}

		private static Totp FromKeePass(PwEntry pe)
		{
			Totp t = new Totp();
			string s;
			if(!string.IsNullOrEmpty(s = Read(pe, "TimeOtp-Secret")))
			{
				t.Key = Encoding.UTF8.GetBytes(s);
				t.SourceFields.Add("TimeOtp-Secret");
			}
			else if(!string.IsNullOrEmpty(s = Read(pe, "TimeOtp-Secret-Hex")))
			{
				t.Key = FromHex(s);
				t.SourceFields.Add("TimeOtp-Secret-Hex");
			}
			else if(!string.IsNullOrEmpty(s = Read(pe, "TimeOtp-Secret-Base32")))
			{
				t.Key = FromBase32(s);
				t.SourceFields.Add("TimeOtp-Secret-Base32");
			}
			else if(!string.IsNullOrEmpty(s = Read(pe, "TimeOtp-Secret-Base64")))
			{
				t.Key = Convert.FromBase64String(s);
				t.SourceFields.Add("TimeOtp-Secret-Base64");
			}
			else return null;

			int v;
			if(int.TryParse(Read(pe, "TimeOtp-Length") ?? string.Empty, out v)) t.Digits = v;
			if(int.TryParse(Read(pe, "TimeOtp-Period") ?? string.Empty, out v)) t.Period = v;
			string alg = Read(pe, "TimeOtp-Algorithm");
			if(!string.IsNullOrEmpty(alg)) t.Algorithm = NormAlg(alg);
			foreach(string f in new string[] { "TimeOtp-Length", "TimeOtp-Period", "TimeOtp-Algorithm" })
			{
				if(pe.Strings.Get(f) != null) t.SourceFields.Add(f);
			}
			return t;
		}

		private static Totp FromOtpField(PwEntry pe)
		{
			string fieldName = null;
			string s = null;
			foreach(KeyValuePair<string, ProtectedString> kvp in pe.Strings)
			{
				if(string.Equals(kvp.Key, "otp", StringComparison.OrdinalIgnoreCase))
				{
					fieldName = kvp.Key;
					s = kvp.Value.ReadString().Trim();
					break;
				}
			}
			if(string.IsNullOrEmpty(s)) return null;

			Totp t = new Totp();
			t.SourceFields.Add(fieldName);
			string query = s;
			if(s.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
			{
				if(s.IndexOf("otpauth://hotp", StringComparison.OrdinalIgnoreCase) == 0) return null;
				int q = s.IndexOf('?');
				query = ((q >= 0) ? s.Substring(q + 1) : string.Empty);
			}
			else if(s.IndexOf('=') < 0)
			{
				// Nur das Geheimnis (Base32)
				t.Key = FromBase32(s);
				return t;
			}

			foreach(string part in query.Split('&'))
			{
				int eq = part.IndexOf('=');
				if(eq <= 0) continue;
				string k = part.Substring(0, eq).Trim().ToLowerInvariant();
				string v = Uri.UnescapeDataString(part.Substring(eq + 1).Trim());
				int n;
				if((k == "secret") || (k == "key")) t.Key = FromBase32(v);
				else if(((k == "digits") || (k == "size")) && int.TryParse(v, out n)) t.Digits = n;
				else if(((k == "period") || (k == "step")) && int.TryParse(v, out n)) t.Period = n;
				else if((k == "algorithm") || (k == "otphashmode")) t.Algorithm = NormAlg(v);
				else if((k == "type") && !string.Equals(v, "totp", StringComparison.OrdinalIgnoreCase)) return null;
			}
			return t;
		}

		private static string NormAlg(string s)
		{
			string a = s.ToUpperInvariant().Replace("HMAC", string.Empty).Replace("-", string.Empty).Trim();
			if(a.Contains("512")) return "SHA512";
			if(a.Contains("256")) return "SHA256";
			return "SHA1";
		}

		/// <summary>Aktueller Code und verbleibende Sekunden bis zum nächsten Wechsel.</summary>
		public string Generate(DateTime utcNow, out int secondsLeft)
		{
			long unix = (long)(utcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
			long counter = unix / Period;
			secondsLeft = (int)(Period - (unix % Period));

			byte[] msg = BitConverter.GetBytes(counter);
			if(BitConverter.IsLittleEndian) Array.Reverse(msg);

			byte[] hash;
			using(HMAC h = CreateHmac())
			{
				hash = h.ComputeHash(msg);
			}
			int off = hash[hash.Length - 1] & 0x0F;
			long bin = ((hash[off] & 0x7F) << 24) | (hash[off + 1] << 16) | (hash[off + 2] << 8) | hash[off + 3];
			long mod = 1;
			for(int i = 0; i < Digits; ++i) mod *= 10;
			string code = (bin % mod).ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
			return code;
		}

		/// <summary>Code mit Leerzeichen in der Mitte zur besseren Lesbarkeit (482 913).</summary>
		public static string Pretty(string code)
		{
			if(string.IsNullOrEmpty(code) || (code.Length < 6)) return code;
			int half = code.Length / 2;
			return code.Substring(0, half) + " " + code.Substring(half);
		}

		private HMAC CreateHmac()
		{
			if(Algorithm == "SHA512") return new HMACSHA512(Key);
			if(Algorithm == "SHA256") return new HMACSHA256(Key);
			return new HMACSHA1(Key);
		}

		private static byte[] FromHex(string s)
		{
			s = s.Replace(" ", string.Empty);
			if((s.Length % 2) != 0) return null;
			byte[] b = new byte[s.Length / 2];
			for(int i = 0; i < b.Length; ++i)
				b[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			return b;
		}

		private static byte[] FromBase32(string s)
		{
			const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
			s = s.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).TrimEnd('=').ToUpperInvariant();
			List<byte> res = new List<byte>();
			int buffer = 0, bits = 0;
			foreach(char c in s)
			{
				int v = Alphabet.IndexOf(c);
				if(v < 0) return null;
				buffer = (buffer << 5) | v;
				bits += 5;
				if(bits >= 8)
				{
					bits -= 8;
					res.Add((byte)((buffer >> bits) & 0xFF));
				}
			}
			return res.ToArray();
		}
	}
}
