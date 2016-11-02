using System;
using System.Xml.Serialization;
using VRage.Serialization;
using VRage.Utils;

namespace HoverRail {
	using StoreType = SerializableDictionary<string, string>;
	static class SettingsStore {
		const string KeyName = "HoverRailSettings";
		static StoreType store;
		static SettingsStore() {
			SettingsStore.store = new StoreType();
		}
		static bool data_loaded = false;
		public static T Get<T>(IMyEntity entity, string prop, T deflt) {
			if (!data_loaded) Load();
			var key = String.Format("{0}.{1}", entity.EntityId, prop);
			var dict = SettingsStore.store.Dictionary;
			if (!dict.ContainsKey(key)) return deflt;
			return (T)Convert.ChangeType(dict[key], typeof(T));
		}
		public static void Set(IMyEntity entity, string prop, object value) {
			var key = String.Format("{0}.{1}", entity.EntityId, prop);
			var dict = SettingsStore.store.Dictionary;
			if (!dict.ContainsKey(key)) dict.Add(key, value.ToString());
			else dict[key] = value.ToString();
			Save(); // euugh
		}
		public static void Load() {
			MyLog.Default.WriteLine("HoverRail loading settings.");
			data_loaded = true;
			string value;
			if (!MyAPIGateway.Utilities.GetVariable(SettingsStore.KeyName, out value)) {
				MyLog.Default.WriteLine("No settings found.");
				return;
			}
			SettingsStore.store = MyAPIGateway.Utilities.SerializeFromXML<StoreType>(value);
			MyLog.Default.WriteLine("HoverRail done loading.");
		}
		public static void Save() {
			MyLog.Default.WriteLine("HoverRail saving settings.");
			string serialized = MyAPIGateway.Utilities.SerializeToXML(SettingsStore.store);
			// MyLog.Default.WriteLine(String.Format("debug: {0}", serialized));
			MyAPIGateway.Utilities.SetVariable(SettingsStore.KeyName, serialized);
			MyLog.Default.WriteLine("HoverRail done saving.");
		}
	}
}
