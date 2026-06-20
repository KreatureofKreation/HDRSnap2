using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HDRSnap2
{
    // Persisted user settings (currently just the capture hotkey).
    // Stored in %LOCALAPPDATA%\HDRSnap2\settings.json so it works even when the
    // exe lives in a read-only location like Program Files.
    public class Settings
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        public uint Modifiers { get; set; } = MOD_CONTROL | MOD_ALT;
        public uint Key { get; set; } = 0x51; // 'Q'

        private static string Dir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HDRSnap2");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Settings load: " + ex.Message); }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Settings save: " + ex.Message); }
        }

        public string HotkeyText => HotkeyToText(Modifiers, Key);

        public static string HotkeyToText(uint mods, uint vk)
        {
            var parts = new List<string>();
            if ((mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((mods & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyName(vk));
            return string.Join("+", parts);
        }

        public static string KeyName(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();       // A-Z
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();       // 0-9
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x6F);           // F1-F12
            return vk switch
            {
                0x20 => "Space",
                0x2C => "PrtScn",
                0x2D => "Insert",
                0x2E => "Delete",
                0x24 => "Home",
                0x23 => "End",
                0x21 => "PageUp",
                0x22 => "PageDown",
                _ => "0x" + vk.ToString("X2")
            };
        }
    }
}
