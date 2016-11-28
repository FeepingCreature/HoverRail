using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using VRage.Serialization;
using VRage.Utils;

namespace HoverRail {
	static class SettingsStore {
		const ushort HOVERRAIL_MESSAGE_ID = 59619; // see http://www.spaceengineerswiki.com/index.php?title=Registry_of_Message_Ids
		const string KeyName = "HoverRailSettings_v2"; // format changed, from xml to cfg (good riddance)
		
		// network marker - message indicating a change of a setting to be shared
		// format:
		// HoverRail Change
		// key1=value1
		// key2=value2
		// ...
		const string CHANGE_MARKER = "HoverRail Change";
		
		const string REBROADCAST_MARKER = "HoverRail Rebroadcast"; // break loops
		
		static Dictionary<long, Dictionary<string, string>> dict;
		static SettingsStore() {
			dict = new Dictionary<long, Dictionary<string, string>>();
		}
		static bool data_loaded = false;
		public static T Get<T>(IMyEntity entity, string prop, T deflt) {
			if (!data_loaded) Load();
			if (!dict.ContainsKey(entity.EntityId)) return deflt;
			var dict2 = dict[entity.EntityId];
			if (!dict2.ContainsKey(prop)) return deflt;
			return (T)Convert.ChangeType(dict2[prop], typeof(T));
		}
		private static void SetKeyInternal(long id, string prop, string value) {
			if (!dict.ContainsKey(id)) {
				// first time seeing this entity
				// set it up with our Close handler so we remove its settings when it gets removed
				// (note: we can't use IMyEntity as a parameter because id might come from string parsing)
				IMyEntity entity = null;
				// only the server has the view of all entities
				if (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer) {
					if (!MyAPIGateway.Entities.TryGetEntityById(id, out entity)) {
						MyLog.Default.WriteLine(String.Format("WARNING(server): tried to set setting for unknown entity {0}, ignoring", id));
						return;
					}
					entity.OnClose += HandleClose;
				}
				dict.Add(id, new Dictionary<string, string>());
				// MyLog.Default.WriteLine(String.Format("DEBUG: setting up OnClose for {0}", id));
			}
			var dict2 = dict[id];
			if (!dict2.ContainsKey(prop)) dict2.Add(prop, value);
			else dict2[prop] = value;
		}
		public static void SetKey(long id, string prop, string value, bool share = true) {
			SetKeyInternal(id, prop, value);
			Save(); // euugh
			// TODO figure out what about server triggered events?
			if (share && !MyAPIGateway.Multiplayer.IsServer) {
				var change_message = Encoding.UTF8.GetBytes(String.Format("{0}\n{1}.{2}={3}", CHANGE_MARKER, id, prop, value));
				MyLog.Default.WriteLine(String.Format("client> {0}.{1}={2}", id, prop, value));
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
					// parse 12345.foo=True
					var pair = change.Split(new char[] {'='}, 2);
					string key = pair[0], value = pair[1];
					var prop_id_pair = key.Split(new char[] {'.'}, 2);
					string id_str = prop_id_pair[0], prop = prop_id_pair[1];
					long id = Int64.Parse(id_str);
					SetKey(id, prop, value, false);
				}
				if (MyAPIGateway.Multiplayer.IsServer) {
					var rebroadcast_message = Encoding.UTF8.GetBytes(String.Format("{0}\n{1}", REBROADCAST_MARKER, str_message));
					// rebroadcast
					// MyLog.Default.WriteLine("server> rebroadcast");
					MyAPIGateway.Multiplayer.SendMessageToOthers(HOVERRAIL_MESSAGE_ID, rebroadcast_message, true);
				}
			}
		}
		public static void Set(IMyEntity entity, string prop, object value) {
			var value_str = value.ToString();
			SetKey(entity.EntityId, prop, value_str);
		}
		public static void Load() {
			LoadOldSettings("HoverRailSettings");
			MyLog.Default.WriteLine("HoverRail loading settings.");
			data_loaded = true;
			string serialized;
			if (!MyAPIGateway.Utilities.GetVariable(KeyName, out serialized)) {
				MyLog.Default.WriteLine("No settings found.");
				return;
			}
			IEnumerable<string> lines = serialized.Split('\n');
			int num_ents = 0, num_settings = 0;
			long current_id = -1;
			foreach (string line in lines) {
				if (line.StartsWith("[")) {
					string id_str = line.Split('[', ']')[1];
					current_id = Int64.Parse(id_str);
					num_ents ++;
				} else if (line.Contains("=")) {
					if (current_id == -1) continue; // wrong order!
					var pair = line.Split(new char[] {'='}, 2);
					string prop = pair[0], value = pair[1];
					SetKeyInternal(current_id, prop, value);
					num_settings ++;
				}
			}
			MyLog.Default.WriteLine(String.Format("HoverRail loaded {0} entities, {1} settings.", num_ents, num_settings));
		}
		public static void HandleClose(IMyEntity entity) {
			MyLog.Default.WriteLine(String.Format("HoverRail entity {0} removed from world, deleting settings.", entity.EntityId));
			dict.Remove(entity.EntityId);
			Save();
		}
		public static void Save() {
			// MyLog.Default.WriteLine("HoverRail saving settings.");
			var builder = new StringBuilder();
			foreach (var entry in dict) {
				builder.AppendFormat("[{0}]\n", entry.Key);
				foreach (var entry2 in entry.Value) {
					builder.AppendFormat("{0}={1}\n", entry2.Key, entry2.Value);
				}
			}
			var serialized = builder.ToString();
			// MyLog.Default.WriteLine(String.Format("DEBUG: {0}", serialized));
			MyAPIGateway.Utilities.SetVariable(KeyName, serialized);
			// MyLog.Default.WriteLine("HoverRail done saving.");
		}
		// load legacy xml format settings
		public static void LoadOldSettings(string old_key) {
			string value;
			if (!MyAPIGateway.Utilities.GetVariable(old_key, out value)) {
				return;
			}
			var store = MyAPIGateway.Utilities.SerializeFromXML<SerializableDictionary<string, string>>(value);
			int num_entries = 0;
			foreach (var entry in store.Dictionary) {
				string key = entry.Key;
				var prop_id_pair = key.Split(new char[] {'.'}, 2);
				string id_str = prop_id_pair[0], prop = prop_id_pair[1];
				long id = Int64.Parse(id_str);
				SetKeyInternal(id, prop, entry.Value);
				num_entries ++;
			}
			Save();
			MyLog.Default.WriteLine(String.Format("HoverRail converted {0} legacy entries.", num_entries));
			// now we can safely clean up
			MyAPIUtilities.Static.Variables.Remove(old_key);
		}
	}
}
