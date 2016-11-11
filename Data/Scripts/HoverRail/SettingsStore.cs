using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRage.Serialization;
using VRage.Utils;

namespace HoverRail {
	using StoreType = SerializableDictionary<string, string>;
	static class SettingsStore {
		const ushort HOVERRAIL_MESSAGE_ID = 59619; // see http://www.spaceengineerswiki.com/index.php?title=Registry_of_Message_Ids
		const string KeyName = "HoverRailSettings";
		
		// network marker - message indicating a change of a setting to be shared
		// format:
		// HoverRail Change
		// key1=value1
		// key2=value2
		// ...
		const string CHANGE_MARKER = "HoverRail Change";
		
		const string REBROADCAST_MARKER = "HoverRail Rebroadcast"; // break loops
		
		static StoreType store;
		static SettingsStore() {
			store = new StoreType();
		}
		static bool data_loaded = false;
		public static T Get<T>(IMyEntity entity, string prop, T deflt) {
			if (!data_loaded) Load();
			var key = String.Format("{0}.{1}", entity.EntityId, prop);
			var dict = store.Dictionary;
			if (!dict.ContainsKey(key)) return deflt;
			return (T)Convert.ChangeType(dict[key], typeof(T));
		}
		public static void SetKey(string key, string value, bool share = true) {
			var dict = store.Dictionary;
			if (!dict.ContainsKey(key)) dict.Add(key, value);
			else dict[key] = value;
			Save(); // euugh
			if (share && !MyAPIGateway.Multiplayer.IsServer) {
				var change_message = Encoding.UTF8.GetBytes(String.Format("{0}\n{1}={2}", CHANGE_MARKER, key, value));
				MyLog.Default.WriteLine(String.Format("client> {0}={1}", key, value));
				MyAPIGateway.Multiplayer.SendMessageToServer(HOVERRAIL_MESSAGE_ID, change_message, true);
			}
		}
		public static void SetupNetworkHandlers() {
			MyAPIGateway.Multiplayer.RegisterMessageHandler(HOVERRAIL_MESSAGE_ID, HandleMessage);
		}
		public static bool ByteStartsWith(byte[] message, byte[] cmp) {
			if (message.Length < cmp.Length) return false;
			for (int i = 0; i < cmp.Length; i++) {
				if (message[i] != cmp[i]) return false;
			}
			return true;
		}
		public static void HandleMessage(byte[] data) {
			var b_rebroadcast_marker = Encoding.UTF8.GetBytes(REBROADCAST_MARKER);
			if (ByteStartsWith(data, b_rebroadcast_marker)) {
				if (MyAPIGateway.Multiplayer.IsServer) return; // discard
				
				var str_message = Encoding.UTF8.GetString(data);
				// strip rebroadcast line
				str_message = str_message.Split(new char[] {'\n'}, 2)[1];
				data = Encoding.UTF8.GetBytes(str_message);
			}
			
			var b_change_marker = Encoding.UTF8.GetBytes(CHANGE_MARKER);
			if (ByteStartsWith(data, b_change_marker)) {
				var str_message = Encoding.UTF8.GetString(data);
				IEnumerable<string> changes = str_message.Split('\n').Skip(1);
				foreach (var change in changes) {
					var pair = change.Split(new char[] {'='}, 2);
					string key = pair[0], value = pair[1];
					SetKey(key, value, false);
				}
				if (MyAPIGateway.Multiplayer.IsServer) {
					var rebroadcast_message = Encoding.UTF8.GetBytes(String.Format("{0}\n{1}", REBROADCAST_MARKER, str_message));
					// rebroadcast
					MyLog.Default.WriteLine("server> rebroadcast");
					MyAPIGateway.Multiplayer.SendMessageToOthers(HOVERRAIL_MESSAGE_ID, rebroadcast_message, true);
				}
			}
		}
		public static void Set(IMyEntity entity, string prop, object value) {
			var key = String.Format("{0}.{1}", entity.EntityId, prop);
			var dict = store.Dictionary;
			var value_str = value.ToString();
			SetKey(key, value_str);
		}
		public static void Load() {
			MyLog.Default.WriteLine("HoverRail loading settings.");
			data_loaded = true;
			string value;
			if (!MyAPIGateway.Utilities.GetVariable(KeyName, out value)) {
				MyLog.Default.WriteLine("No settings found.");
				return;
			}
			store = MyAPIGateway.Utilities.SerializeFromXML<StoreType>(value);
			MyLog.Default.WriteLine("HoverRail done loading.");
		}
		public static void Save() {
			MyLog.Default.WriteLine("HoverRail saving settings.");
			string serialized = MyAPIGateway.Utilities.SerializeToXML(store);
			// MyLog.Default.WriteLine(String.Format("debug: {0}", serialized));
			MyAPIGateway.Utilities.SetVariable(KeyName, serialized);
			MyLog.Default.WriteLine("HoverRail done saving.");
		}
	}
}
