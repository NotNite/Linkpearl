// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Utility/GameConfig.cs
using System;
using System.Collections.Generic;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Configuration;

namespace Linkpearl;

public static unsafe class GameConfig {
    public static GameConfigSection System;
    public static GameConfigSection UiConfig;
    public static GameConfigSection UiControl;

    static GameConfig() {
        System = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.ConfigBase,
            new[] { "PadMode" });
        UiConfig = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiConfig);
        UiControl = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlConfig);
    }

    public class EntryWrapper {
        public EntryWrapper(ConfigEntry* entry, string name) {
            this.Name = name;
            this.Entry = entry;
        }

        public string Name { get; }
        public ConfigEntry* Entry { get; }

        public object? Value {
            get {
                return this.Entry->Type switch {
                    2 => this.Entry->Value.UInt,
                    3 => this.Entry->Value.Float,
                    4 => this.Entry->Value.String->ToString(),
                    _ => null
                };
            }
            set {
                switch (this.Entry->Type) {
                    case 2 when value is uint u32: {
                        if (!this.Entry->SetValueUInt(u32)) throw new Exception("Failed");

                        break;
                    }
                    case 3 when value is float f: {
                        if (!this.Entry->SetValueFloat(f)) throw new Exception("Failed");

                        break;
                    }
                    case 4 when value is string s: {
                        if (!this.Entry->SetValueString(s)) throw new Exception("Failed");

                        break;
                    }
                    default:
                        throw new ArgumentException("Invalid Value");
                }
            }
        }
    }

    public class GameConfigSection {
        private readonly ConfigBase* configBase;

        private readonly Dictionary<string, uint> indexMap = new();
        private readonly Dictionary<uint, string> nameMap = new();

        private string[] ignoredNames = Array.Empty<string>();

        public GameConfigSection(ConfigBase* configBase, string[] ignoredNames = null) {
            this.configBase = configBase;

            if (ignoredNames != null) this.ignoredNames = ignoredNames;

            var e = configBase->ConfigEntry;
            for (var i = 0U; i < configBase->ConfigCount; i++, e++) {
                if (e->Name == null) continue;
                var eName = MemoryHelper.ReadStringNullTerminated(new nint(e->Name));
                if (!this.indexMap.ContainsKey(eName)) this.indexMap.Add(eName, i);
            }
        }

        public uint ConfigCount => this.configBase->ConfigCount;

        public EntryWrapper? this[uint i] {
            get {
                if (i >= this.configBase->ConfigCount) return null;

                var e = this.configBase->ConfigEntry;
                e += i;
                if (e->Name == null) return null;

                if (!this.nameMap.TryGetValue(i, out var name)) {
                    name = MemoryHelper.ReadStringNullTerminated(new nint(e->Name));
                    this.nameMap.TryAdd(i, name);
                    this.indexMap.TryAdd(name, i);
                }

                return new EntryWrapper(e, name);
            }
        }

        public EntryWrapper? this[string name] {
            get {
                if (!this.TryGetIndex(name, out var i)) return null;
                var e = this.configBase->ConfigEntry;
                e += i;
                if (e->Name == null) return null;
                return new EntryWrapper(e, name);
            }
        }

        public bool TryGetEntry(string name, out EntryWrapper result, StringComparison? nameComparison = null) {
            result = null;
            if (!this.TryGetIndex(name, out var i, nameComparison)) return false;
            var e = this.configBase->ConfigEntry;
            e += i;
            if (e->Name == null) return false;
            result = new EntryWrapper(e, name);
            return true;
        }

        public bool TryGetName(uint index, out string? name) {
            name = null;
            if (index >= this.configBase->ConfigCount) return false;
            var hasName = this.nameMap.TryGetValue(index, out name);
            if (hasName) return name != null;
            var e = this.configBase->ConfigEntry;
            e += index;
            if (e->Name == null) return false;
            name = MemoryHelper.ReadStringNullTerminated(new nint(e->Name));
            this.indexMap.TryAdd(name, index);
            this.nameMap.TryAdd(index, name);
            return true;
        }

        public bool TryGetIndex(string name, out uint index, StringComparison? stringComparison = null) {
            if (this.indexMap.TryGetValue(name, out index)) return true;
            var e = this.configBase->ConfigEntry;
            for (var i = 0U; i < this.configBase->ConfigCount; i++, e++) {
                if (e->Name == null) continue;
                var eName = MemoryHelper.ReadStringNullTerminated(new nint(e->Name));
                if (eName.Equals(name)) {
                    this.indexMap.TryAdd(name, i);
                    this.nameMap.TryAdd(i, name);
                    index = i;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        private bool TryGetEntry(uint index, out ConfigEntry* entry) {
            entry = null;
            if (this.configBase->ConfigEntry == null || index >= this.configBase->ConfigCount) return false;
            entry = this.configBase->ConfigEntry;
            entry += index;
            return true;
        }

        public bool TryGetBool(string name, out bool value) {
            value = false;
            if (!this.TryGetIndex(name, out var index)) return false;
            if (!this.TryGetEntry(index, out var entry)) return false;
            value = entry->Value.UInt != 0;
            return true;
        }

        public bool GetBool(string name) {
            if (!this.TryGetBool(name, out var value)) throw new Exception($"Failed to get Bool '{name}'");
            return value;
        }

        public void Set(string name, bool value) {
            if (!this.TryGetIndex(name, out var index)) return;
            if (!this.TryGetEntry(index, out var entry)) return;
            entry->SetValue(value ? 1U : 0U);
        }

        public bool TryGetUInt(string name, out uint value) {
            value = 0;
            if (!this.TryGetIndex(name, out var index)) return false;
            if (!this.TryGetEntry(index, out var entry)) return false;
            value = entry->Value.UInt;
            return true;
        }

        public uint GetUInt(string name) {
            if (!this.TryGetUInt(name, out var value)) throw new Exception($"Failed to get UInt '{name}'");
            return value;
        }

        public void Set(string name, uint value) {
            Plugin.Framework.RunOnFrameworkThread(() => {
                if (!this.TryGetIndex(name, out var index)) return;
                if (!this.TryGetEntry(index, out var entry)) return;
                entry->SetValue(value);
            });
        }

        public bool TryGetFloat(string name, out float value) {
            value = 0;
            if (!this.TryGetIndex(name, out var index)) return false;
            if (!this.TryGetEntry(index, out var entry)) return false;
            value = entry->Value.Float;
            return true;
        }

        public float GetFloat(string name) {
            if (!this.TryGetFloat(name, out var value)) throw new Exception($"Failed to get Float '{name}'");
            return value;
        }

        public void Set(string name, float value) {
            Plugin.Framework.RunOnFrameworkThread(() => {
                if (!this.TryGetIndex(name, out var index)) return;
                if (!this.TryGetEntry(index, out var entry)) return;
                entry->SetValue(value);
            });
        }

        public bool TryGetString(string name, out string value) {
            value = string.Empty;
            if (!this.TryGetIndex(name, out var index)) return false;
            if (!this.TryGetEntry(index, out var entry)) return false;
            if (entry->Type != 4) return false;
            if (entry->Value.String == null) return false;
            value = entry->Value.String->ToString();
            return true;
        }

        public string GetString(string name) {
            if (!this.TryGetString(name, out var value)) throw new Exception($"Failed to get String '{name}'");
            return value;
        }

        public void Set(string name, string value) {
            Plugin.Framework.RunOnFrameworkThread(() => {
                if (!this.TryGetIndex(name, out var index)) return;
                if (!this.TryGetEntry(index, out var entry)) return;
                entry->SetValue(value);
            });
        }
    }
}
