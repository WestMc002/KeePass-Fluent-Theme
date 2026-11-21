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
		// Der Schlüssel liegt nur verschlüsselt im Speicher (wie die Felder in KeePass) und wird
		// allein für die Berechnung eines Codes kurz entschlüsselt
		private ProtectedBinary m_key;

		/// <summary>Schlüssel setzen; das übergebene Feld wird danach geleert.</summary>
		public byte[] Key
		{
			set
			{
				m_key = (((value != null) && (value.Length > 0)) ? new ProtectedBinary(true, value) : null);
				if(value != null) Array.Clear(value, 0, value.Length);
			}
		}

		public bool HasKey
		{
			get { return ((m_key != null) && (m_key.Length > 0)); }
		}
		public int Digits = 6;
		public int Period = 30;
		public string Algorithm = "SHA1";

		/// <summary>Felder, aus denen das Geheimnis stammt (werden unter „Weitere Felder“ ausgeblendet).</summary>
		public readonly List<string> SourceFields = new List<string>();

		/// <summary>Felder, die einen Einmalcode-Schlüssel enthalten (KeePass, KeePassXC, KeeOtp,
		/// KeeTrayTOTP). Sie erscheinen nie unter „Weitere Felder“ – auch dann nicht, wenn sich aus
		/// ihnen kein Code berechnen lässt.</summary>
		public static bool IsSecretField(string name)
		{
			if(string.IsNullOrEmpty(name)) return false;
			return (name.StartsWith("TimeOtp-Secret", StringComparison.OrdinalIgnoreCase) ||
				name.StartsWith("HmacOtp-Secret", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(name, "otp", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(name, "TOTP Seed", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Hat der Eintrag ein Feld für zeitbasierte Einmalcodes (auch wenn es ungültig ist)?</summary>
		public static bool HasTotpField(PwEntry pe)
		{
			if(pe == null) return false;
			foreach(KeyValuePair<string, ProtectedString> kvp in pe.Strings)
			{
				if(kvp.Key.StartsWith("TimeOtp-Secret", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(kvp.Key, "otp", StringComparison.OrdinalIgnoreCase))
				{
					if(!kvp.Value.IsEmpty) return true;
				}
			}
			return false;
		}

		public static Totp FromEntry(PwEntry pe)
		{
			if(pe == null) return null;
			try
			{
				Totp t = FromKeePass(pe);
				if(t == null) t = FromOtpField(pe);
				if((t == null) || !t.HasKey) return null;
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

		// Die Schlüssel werden als Zeichenfeld gelesen und direkt in ein Bytefeld dekodiert; beide
		// werden danach geleert. Es entsteht keine Zeichenkette mit dem Schlüssel (die ließe sich
		// nicht gezielt löschen).

		private static char[] ReadSecret(PwEntry pe, string key)
		{
			ProtectedString ps = pe.Strings.Get(key);
			if((ps == null) || ps.IsEmpty) return null;
			return ps.ReadChars();
		}

		private static void Wipe(char[] c)
		{
			if(c != null) Array.Clear(c, 0, c.Length);
		}

		/// <summary>Bereich ohne Leerraum am Anfang und Ende (end zeigt hinter das letzte Zeichen).</summary>
		private static void TrimRange(char[] c, out int start, out int end)
		{
			start = 0;
			end = c.Length;
			while((start < end) && char.IsWhiteSpace(c[start])) ++start;
			while((end > start) && char.IsWhiteSpace(c[end - 1])) --end;
		}

		private static int IndexOf(char[] c, int start, int end, char what)
		{
			for(int i = start; i < end; ++i)
			{
				if(c[i] == what) return i;
			}
			return -1;
		}

		/// <summary>Ist der Bereich (ohne Leerraum außen, Groß-/Kleinschreibung egal) genau dieser Name?</summary>
		private static bool NameIs(char[] c, int start, int end, string lowerName)
		{
			while((start < end) && char.IsWhiteSpace(c[start])) ++start;
			while((end > start) && char.IsWhiteSpace(c[end - 1])) --end;
			if((end - start) != lowerName.Length) return false;
			for(int i = 0; i < lowerName.Length; ++i)
			{
				if(char.ToLowerInvariant(c[start + i]) != lowerName[i]) return false;
			}
			return true;
		}

		private static bool StartsWith(char[] c, int start, int end, string prefix)
		{
			if(end - start < prefix.Length) return false;
			for(int i = 0; i < prefix.Length; ++i)
			{
				if(char.ToLowerInvariant(c[start + i]) != prefix[i]) return false;
			}
			return true;
		}

		private static Totp FromKeePass(PwEntry pe)
		{
			Totp t = new Totp();
			char[] c = null;
			try
			{
				int a, b;
				if((c = ReadSecret(pe, "TimeOtp-Secret")) != null)
				{
					TrimRange(c, out a, out b);
					t.Key = Encoding.UTF8.GetBytes(c, a, b - a);
					t.SourceFields.Add("TimeOtp-Secret");
				}
				else if((c = ReadSecret(pe, "TimeOtp-Secret-Hex")) != null)
				{
					TrimRange(c, out a, out b);
					t.Key = FromHex(c, a, b);
					t.SourceFields.Add("TimeOtp-Secret-Hex");
				}
				else if((c = ReadSecret(pe, "TimeOtp-Secret-Base32")) != null)
				{
					TrimRange(c, out a, out b);
					t.Key = FromBase32(c, a, b);
					t.SourceFields.Add("TimeOtp-Secret-Base32");
				}
				else if((c = ReadSecret(pe, "TimeOtp-Secret-Base64")) != null)
				{
					TrimRange(c, out a, out b);
					t.Key = Convert.FromBase64CharArray(c, a, b - a);
					t.SourceFields.Add("TimeOtp-Secret-Base64");
				}
				else return null;
			}
			finally { Wipe(c); }

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
			ProtectedString ps = null;
			foreach(KeyValuePair<string, ProtectedString> kvp in pe.Strings)
			{
				if(string.Equals(kvp.Key, "otp", StringComparison.OrdinalIgnoreCase))
				{
					fieldName = kvp.Key;
					ps = kvp.Value;
					break;
				}
			}
			if((ps == null) || ps.IsEmpty) return null;

			char[] c = ps.ReadChars();
			try
			{
				int start, end;
				TrimRange(c, out start, out end);
				if(start >= end) return null;

				Totp t = new Totp();
				t.SourceFields.Add(fieldName);
				int pos = start;
				if(StartsWith(c, start, end, "otpauth://"))
				{
					if(StartsWith(c, start, end, "otpauth://hotp")) return null;
					int q = IndexOf(c, start, end, '?');
					pos = ((q >= 0) ? (q + 1) : end);
				}
				else if((IndexOf(c, start, end, '=') < 0) || IsBareBase32(c, start, end))
				{
					// Nur das Geheimnis (Base32), auch mit Füllzeichen „=“ am Ende
					t.Key = FromBase32(c, start, end);
					return t;
				}

				while(pos < end)
				{
					int amp = IndexOf(c, pos, end, '&');
					int partEnd = ((amp >= 0) ? amp : end);
					int eq = IndexOf(c, pos, partEnd, '=');
					if(eq > pos)
					{
						// Den Namen direkt im Puffer mit den bekannten Namen vergleichen – bei einem
						// fehlerhaften Wert könnte vor dem „=“ auch der Schlüssel selbst stehen
						if(NameIs(c, pos, eq, "secret") || NameIs(c, pos, eq, "key"))
						{
							// URL-Kodierung (%3D usw.) im löschbaren Puffer auflösen, dann dekodieren.
							// Ungültige Kodierung: kein Schlüssel, also auch kein (falscher) Code.
							char[] sec = Unescape(c, eq + 1, partEnd);
							try { t.Key = ((sec != null) ? FromBase32(sec, 0, sec.Length) : null); }
							finally { Wipe(sec); }
						}
						else
						{
							bool digits = (NameIs(c, pos, eq, "digits") || NameIs(c, pos, eq, "size"));
							bool period = (NameIs(c, pos, eq, "period") || NameIs(c, pos, eq, "step"));
							bool alg = (NameIs(c, pos, eq, "algorithm") || NameIs(c, pos, eq, "otphashmode"));
							bool type = NameIs(c, pos, eq, "type");
							if(digits || period || alg || type)
							{
								// Nur die Werte bekannter, unkritischer Angaben werden zu Zeichenketten;
								// unbekannte Parameter bleiben unangetastet im Puffer
								string v = Uri.UnescapeDataString(new string(c, eq + 1, partEnd - eq - 1).Trim());
								int n;
								if(digits) { if(int.TryParse(v, out n)) t.Digits = n; }
								else if(period) { if(int.TryParse(v, out n)) t.Period = n; }
								else if(alg) t.Algorithm = NormAlg(v);
								else if(!string.Equals(v, "totp", StringComparison.OrdinalIgnoreCase)) return null;
							}
						}
					}
					pos = partEnd + 1;
				}
				return t;
			}
			finally { Wipe(c); }
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
			byte[] key = m_key.ReadData();
			try
			{
				using(HMAC h = CreateHmac(key))
				{
					hash = h.ComputeHash(msg);
				}
			}
			finally { Array.Clear(key, 0, key.Length); }
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

		private HMAC CreateHmac(byte[] key)
		{
			if(Algorithm == "SHA512") return new HMACSHA512(key);
			if(Algorithm == "SHA256") return new HMACSHA256(key);
			return new HMACSHA1(key);
		}

		private static int HexValue(char ch)
		{
			if((ch >= '0') && (ch <= '9')) return ch - '0';
			if((ch >= 'a') && (ch <= 'f')) return ch - 'a' + 10;
			if((ch >= 'A') && (ch <= 'F')) return ch - 'A' + 10;
			return -1;
		}

		private static byte[] FromHex(char[] c, int start, int end)
		{
			int digits = 0;
			for(int i = start; i < end; ++i)
			{
				if(c[i] == ' ') continue;
				if(HexValue(c[i]) < 0) return null;
				++digits;
			}
			if((digits == 0) || ((digits % 2) != 0)) return null;

			byte[] b = new byte[digits / 2];
			int n = 0, hi = -1;
			for(int i = start; i < end; ++i)
			{
				if(c[i] == ' ') continue;
				int v = HexValue(c[i]);
				if(hi < 0) hi = v;
				else
				{
					b[n++] = (byte)((hi << 4) | v);
					hi = -1;
				}
			}
			return b;
		}

		/// <summary>Besteht der Bereich nur aus einem Base32-Wert (mit Leerzeichen/Bindestrichen und
		/// optionalen Füllzeichen am Ende) – also nicht aus Parametern der Form name=wert?</summary>
		private static bool IsBareBase32(char[] c, int start, int end)
		{
			int i = start, n = 0;
			for(; i < end; ++i)
			{
				if((c[i] == ' ') || (c[i] == '-')) continue;
				if(Base32Value(c[i]) < 0) break;
				++n;
			}
			if(n == 0) return false;
			for(; i < end; ++i)
			{
				if((c[i] != '=') && (c[i] != ' ')) return false;
			}
			return true;
		}

		private static int Base32Value(char ch)
		{
			if((ch >= 'A') && (ch <= 'Z')) return ch - 'A';
			if((ch >= 'a') && (ch <= 'z')) return ch - 'a';
			if((ch >= '2') && (ch <= '7')) return ch - '2' + 26;
			return -1;
		}

		/// <summary>URL-Kodierung (%XX) auflösen, Ergebnis in einem neuen, löschbaren Zeichenfeld.
		/// Ungültige oder unvollständige Kodierung sowie Zeichen außerhalb von ASCII: null.</summary>
		private static char[] Unescape(char[] c, int start, int end)
		{
			char[] res = new char[Math.Max(0, end - start)];
			int n = 0;
			for(int i = start; i < end; ++i)
			{
				if(c[i] != '%')
				{
					res[n++] = c[i];
					continue;
				}
				int hi = ((i + 2 < end) ? HexValue(c[i + 1]) : -1);
				int lo = ((i + 2 < end) ? HexValue(c[i + 2]) : -1);
				int v = (((hi >= 0) && (lo >= 0)) ? ((hi << 4) | lo) : -1);
				if((v < 0x20) || (v > 0x7E))
				{
					Array.Clear(res, 0, res.Length);
					return null;
				}
				res[n++] = (char)v;
				i += 2;
			}
			if(n == res.Length) return res;
			char[] cut = new char[n];
			Array.Copy(res, cut, n);
			Array.Clear(res, 0, res.Length);
			return cut;
		}

		/// <summary>Base32 direkt in ein Bytefeld. Leerzeichen und Bindestriche werden überlesen. Nach
		/// dem ersten Füllzeichen „=“ dürfen nur noch Füllzeichen und Leerzeichen folgen. Ungültige
		/// Zeichen oder Daten hinter den Füllzeichen: null – lieber kein Code als ein falscher.</summary>
		private static byte[] FromBase32(char[] c, int start, int end)
		{
			int stop = end;
			for(int i = start; i < end; ++i)
			{
				if(c[i] == '=') { stop = i; break; }
			}
			int pad = 0;
			for(int i = stop; i < end; ++i)
			{
				if(c[i] == '=') ++pad;
				else if(c[i] != ' ') return null;
			}

			int count = 0;
			for(int i = start; i < stop; ++i)
			{
				if((c[i] == ' ') || (c[i] == '-')) continue;
				if(Base32Value(c[i]) < 0) return null;
				++count;
			}
			if(count == 0) return null;
			// Base32 kennt nur bestimmte Längen: Ein Rest von 1, 3 oder 6 Zeichen kann kein ganzes
			// Byte ergeben (Tippfehler). Sind Füllzeichen angegeben, muss ihre Anzahl genau passen
			// (bei vollständigen Blöcken keines).
			int rest = count % 8;
			if((rest == 1) || (rest == 3) || (rest == 6)) return null;
			if((pad > 0) && ((rest == 0) || (pad != (8 - rest)))) return null;

			byte[] res = new byte[count * 5 / 8];
			int buffer = 0, bits = 0, n = 0;
			for(int i = start; i < stop; ++i)
			{
				if((c[i] == ' ') || (c[i] == '-')) continue;
				buffer = ((buffer << 5) | Base32Value(c[i])) & 0xFFFF;
				bits += 5;
				if(bits >= 8)
				{
					bits -= 8;
					if(n < res.Length) res[n++] = (byte)((buffer >> bits) & 0xFF);
				}
			}
			return res;
		}
	}
}
