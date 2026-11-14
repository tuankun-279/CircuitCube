using System;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Decides whether this device shows ads, from the content manifest: <see cref="CircuitCube.Core.GameRules.adsOn"/>
    /// 0 = nobody, 1 = everybody, 2 = only the devices whose code is listed in
    /// <see cref="CircuitCube.Core.GameRules.adsTesters"/>. The code is shown in the settings card (seven taps on the
    /// version line) and typed into Level Lab, so ads can be tried on chosen devices during a closed test.
    /// </summary>
    public static class AdsGate
    {
        const string FallbackKey = "cc.deviceCodeSeed";
        // no I, O, 0 or 1: the code is read off a screen and typed by hand
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        static string _code;

        /// <summary>Short code of this device, like "K7QM-2XRD". Stays the same across reinstalls where the system allows.</summary>
        public static string DeviceCode
        {
            get
            {
                if (_code != null) return _code;
                string id = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
                {
                    // no system identifier: a random one kept with the player's settings
                    try
                    {
                        id = PlayerPrefs.GetString(FallbackKey, "");
                        if (id.Length == 0) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(FallbackKey, id); PlayerPrefs.Save(); }
                    }
                    catch (Exception) { id = "unknown"; }
                }
                return _code = CodeFrom(id);
            }
        }

        /// <summary>Ads are on for this device under the current rules.</summary>
        public static bool On
        {
            get { var r = GameData.Rules; return Allowed(r.adsOn, r.adsTesters, DeviceCode); }
        }

        /// <summary>Eight characters from a 64-bit FNV-1a hash of <paramref name="id"/>, as "XXXX-XXXX". Pure, for the tests.</summary>
        public static string CodeFrom(string id)
        {
            ulong h = 14695981039346656037UL;
            if (id != null) foreach (char c in id) { h ^= c; h *= 1099511628211UL; }
            var chars = new char[9];
            for (int i = 0, o = 0; i < 8; i++)
            {
                if (i == 4) chars[o++] = '-';
                chars[o++] = Alphabet[(int)(h & 31)];
                h >>= 5;
            }
            return new string(chars);
        }

        /// <summary>
        /// The rule itself. <paramref name="testers"/> is a list of device codes separated by commas, semicolons or
        /// spaces; case and dashes do not matter. Pure, for the tests.
        /// </summary>
        public static bool Allowed(int adsOn, string testers, string code)
        {
            if (adsOn <= 0) return false;
            if (adsOn == 1) return true;
            if (string.IsNullOrEmpty(testers) || string.IsNullOrEmpty(code)) return false;
            string mine = Plain(code);
            foreach (string part in testers.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                if (Plain(part) == mine) return true;
            return false;
        }

        static readonly char[] Separators = { ',', ';', ' ', '\n', '\r', '\t' };

        static string Plain(string code) => code.Replace("-", "").Trim().ToUpperInvariant();
    }
}
