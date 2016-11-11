using Sandbox.ModAPI;
using Sandbox.Common;

namespace HoverRail {
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class HoverRail : MySessionComponentBase {
		bool inited = false;
		public void Init() {
			SettingsStore.SetupNetworkHandlers();
			inited = true;
		}
		public override void UpdateBeforeSimulation() {
			if(inited) return;
			if(MyAPIGateway.Session == null) return;
			Init();
		}
	}
}
