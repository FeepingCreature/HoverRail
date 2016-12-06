using System;
using System.Text;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Common;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.Components;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace HoverRail {
	public class HoverRailEngine : MyGameLogicComponent {
		const float MAX_POWER_USAGE_MW = 1f;
		const float FORCE_POWER_COST_MW_N = 0.0000001f;
		Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder = null;
		SlidingAverageVector avgGuidance, avgCorrectF, avgDampenF;
		MyResourceSinkComponent sinkComp;
		bool block_initialized = false;
		MyEntity3DSoundEmitter engine_sound;
		MySoundPair sound_engine_start, sound_engine_loop;
        public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder) {
			this.avgGuidance = new SlidingAverageVector(0.3);
			this.avgCorrectF = new SlidingAverageVector(0.9);
			this.avgDampenF = new SlidingAverageVector(0.9);
			Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
			this.objectBuilder = objectBuilder;
			this.id = HoverRailEngine.attachcount++;
			this.activeRailGuides = new HashSet<RailGuide>();
			this.sound_engine_start = new MySoundPair("HoverEngine_Startup");
			this.sound_engine_loop = new MySoundPair("HoverEngine_Loop");
			MyEntity3DSoundEmitter.PreloadSound(sound_engine_start);
			MyEntity3DSoundEmitter.PreloadSound(sound_engine_loop);
			this.engine_sound = new MyEntity3DSoundEmitter(Entity as VRage.Game.Entity.MyEntity);
			this.engine_sound.Force3D = true;
			// MyLog.Default.WriteLine(String.Format("ATTACH TO OBJECT {0}", this.id));
			InitPowerComp();
		}
		
		void EngineCustomInfo(IMyTerminalBlock block, StringBuilder builder) {
			builder.AppendLine(String.Format("Required Power: {0}W", EngineUI.SIFormat(power_usage * 1000000)));
			builder.AppendLine(String.Format("Max Required Power: {0}W", EngineUI.SIFormat(MAX_POWER_USAGE_MW * 1000000)));
		}
		
		// init power usage
		void InitPowerComp() {
            Entity.Components.TryGet<MyResourceSinkComponent>(out sinkComp);
            if (sinkComp == null) {
                // MyLog.Default.WriteLine("set up new power sink");
                sinkComp = new MyResourceSinkComponent();
                sinkComp.Init(
                    MyStringHash.GetOrCompute("Thrust"),
                    MAX_POWER_USAGE_MW,
                    GetCurrentPowerDraw
                );
                Entity.Components.Add(sinkComp);
            }
            else
            {
                // MyLog.Default.WriteLine("reuse existing power sink");
                sinkComp.SetRequiredInputFuncByType(MyResourceDistributorComponent.ElectricityId, GetCurrentPowerDraw);
            }
			(Entity as IMyTerminalBlock).AppendingCustomInfo += EngineCustomInfo;
		}
		
		float power_usage = 0.0f;
		float power_ratio_available = 1.0f;
		float GetCurrentPowerDraw() {
			// MyLog.Default.WriteLine(String.Format("report power usage as {0}", power_usage));
			return power_usage;
		}
		public void UpdatePowerUsage(float new_power) {
			if (power_usage == new_power) return;
			power_ratio_available = 1.0f;
			engine_sound.CustomVolume = (float) (1.0 + new_power * 10); // 100KW = 100% volume
			if (new_power > MAX_POWER_USAGE_MW) {
				power_ratio_available = MAX_POWER_USAGE_MW / new_power;
				new_power = MAX_POWER_USAGE_MW;
			}
			// MyLog.Default.WriteLine(String.Format("set power to {0}", new_power));
			power_usage = new_power;
			sinkComp.Update();
		}
		
		public void InitLate() {
			block_initialized = true;
			
			if (!EngineUI.initialized) EngineUI.InitLate();
		}
		static int attachcount;
		int id;
		private int frame = 0;
		private bool last_power_state = false;
		
		HashSet<RailGuide> activeRailGuides;
		
		void QueueLoopSound(MyEntity3DSoundEmitter emitter) {
			emitter.StoppedPlaying -= QueueLoopSound;
			emitter.PlaySingleSound(sound_engine_loop, true);
		}
		public void UpdatePowerState(bool state_on) {
			bool state_changed = last_power_state != state_on;
			last_power_state = state_on;
			
			if (state_on == false) {
				if (state_changed) {
					engine_sound.StoppedPlaying -= QueueLoopSound;
					engine_sound.StopSound(true);
				}
			} else {
				if (state_changed || !engine_sound.IsPlaying) {
					engine_sound.StoppedPlaying -= QueueLoopSound;
					engine_sound.StopSound(true); // ... why??
					engine_sound.PlaySingleSound(sound_engine_start, true);
					engine_sound.StoppedPlaying += QueueLoopSound;
				}
			}
		}
		
		public override void UpdateBeforeSimulation() {
			if (!block_initialized) InitLate();
			
			frame++;
			
			if (frame % 10 == 0) (Entity as IMyTerminalBlock).RefreshCustomInfo();
			
			if (!(bool) SettingsStore.Get(Entity, "power_on", true)) {
				UpdatePowerUsage(0);
				UpdatePowerState(false);
				return;
			}
			
			// this will be one frame late ... but close enough??
			// power requested that can be satisfied by the network * power required that can be requested given our max
			float power_ratio = sinkComp.SuppliedRatioByType(MyResourceDistributorComponent.ElectricityId) * power_ratio_available;
			if (!sinkComp.IsPoweredByType(MyResourceDistributorComponent.ElectricityId)) {
				power_ratio = 0;
			}
			// MyLog.Default.WriteLine(String.Format("power ratio is {0}", power_ratio));
			
			float height = (float) SettingsStore.Get(Entity, "height_offset", 1.25f);
			
			double forceLimit = (double) (float) SettingsStore.Get(Entity, "force_slider", 100000.0f);
			
			var hoverCenter = Entity.WorldMatrix.Translation;
			var searchCenter = Entity.WorldMatrix.Translation + Entity.WorldMatrix.Down * 2.499;
			// DebugDraw.Sphere(searchCenter, 2.5f, Color.Green);
			
			var rail_pos = new Vector3D(0, 0, 0);
			var weight_sum = 0.0f;
			HashSet<RailGuide> lostGuides = new HashSet<RailGuide>();
			RailGuide anyRailGuide = null;
			foreach (var guide in activeRailGuides) {
				if (!guide.getGuidance(hoverCenter, ref rail_pos, ref weight_sum, height)) {
					// lost rail lock
					lostGuides.Add(guide);
					continue;
				}
				anyRailGuide = guide;
			}
			
			foreach (var guide in lostGuides) {
				activeRailGuides.Remove(guide);
			}
			lostGuides.Clear();
			
			if (weight_sum < 0.9f) {
				// not confident in our rail lock, look for possible new rails
				var area = new BoundingSphereD(searchCenter, 2.5);
				var items = MyAPIGateway.Entities.GetEntitiesInSphere(ref area);
				rail_pos = Vector3D.Zero;
				weight_sum = 0.0f;
				foreach (var ent in items) {
					var guide = RailGuide.fromEntity(ent);
					if (guide != null) {
						var test = guide.getGuidance(hoverCenter, ref rail_pos, ref weight_sum, height);
						if (test) {
							activeRailGuides.Add(guide);
							anyRailGuide = guide;
						}
					}
				}
			}
			
			// MyLog.Default.WriteLine(String.Format("{0}:- hovering at {1}", Entity.EntityId, hoverCenter));
			if (activeRailGuides.Count == 0) {
				UpdatePowerUsage(0);
				UpdatePowerState(true); // powered but idle
				return;
			}
			
			// average by weight
			rail_pos /= weight_sum;
			
			var guidance = rail_pos - hoverCenter;
			// MyLog.Default.WriteLine(String.Format("{0}: rail pos is {1}, due to weight correction by {2}; guidance {3}", Entity.EntityId, rail_pos, weight_sum, guidance));
			DebugDraw.Sphere(rail_pos, 0.15f, Color.Blue);
			DebugDraw.Sphere(rail_pos * 0.5 + hoverCenter * 0.5, 0.1f, Color.Blue);
			DebugDraw.Sphere(hoverCenter, 0.1f, Color.Blue);
			
			// DebugDraw.Sphere(searchCenter, 0.1f, Color.Green);
			
			float force_magnitude = 0;
			// correction force, pushes engine towards rail guide
			{
				var len = guidance.Length() / 2.5; // 0 .. 1
				if (len > 0.001) {
					var weight = len;
					if (weight > 0.99) weight = 0.99; // always some force
					const double splitPoint = 0.5;
					if (weight > splitPoint) weight = 1.0 - (weight - splitPoint) / (1.0 - splitPoint);
					else weight = weight / splitPoint;
					var factor = Math.Pow(weight, 2.0); // spiken
					var guidanceForce = forceLimit * Vector3D.Normalize(guidance) * factor;
					this.avgCorrectF.update(guidanceForce);
					DebugDraw.Sphere(searchCenter, 0.1f, Color.Yellow);
					anyRailGuide.applyForces(Entity, this.avgCorrectF.value * power_ratio);
					force_magnitude += (float) this.avgCorrectF.value.Length();
				}
			}
			// dampening force, reduces oscillation over time
			var dF = guidance - this.avgGuidance.value;
			{
				// var len = guidance.Length() / 2.5;
				// if (len > 0.99) len = 0.99;
				// var factor = Math.Pow(len, 0.3);
				var factor = 1.0;
				var dampenForce = forceLimit * 0.5 * dF * factor; // separate slider?
				this.avgDampenF.update(dampenForce);
				DebugDraw.Sphere(searchCenter + this.avgDampenF.value * 0.000001f, 0.1f, Color.Red);
				anyRailGuide.applyForces(Entity, this.avgDampenF.value * power_ratio);
				force_magnitude += (float) this.avgDampenF.value.Length();
			}
			this.avgGuidance.update(guidance);
			UpdatePowerUsage(force_magnitude * FORCE_POWER_COST_MW_N);
			UpdatePowerState(true);
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false) {
			return objectBuilder;
		}
	}
	
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), "HoverRail_Engine_Large")]
	public class HoverRailEngineLarge : HoverRailEngine {
	}
	// small needs a different name in blender than the large engine
	// but the exporter also appends _Small, so...
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), "HoverRail_Engine_Small_Small")]
	public class HoverRailEngineSmall : HoverRailEngine {
	}

	static class EngineUI {
		public static bool initialized = false;
		public static IMyTerminalControlSlider forceSlider, heightSlider;
		public static IMyTerminalControlOnOffSwitch powerSwitch;
		public static IMyTerminalAction lowerHeightAction, raiseHeightAction;
		public static IMyTerminalAction turnOnAction, turnOffAction, turnOnOffAction;
		
		public static bool BlockIsEngine(IMyTerminalBlock block) {
			return block.BlockDefinition.SubtypeId == "HoverRail_Engine_Large"
			    || block.BlockDefinition.SubtypeId == "HoverRail_Engine_Small_Small";
		}
		public static float LogRound(float f) {
			var logbase = Math.Pow(10, Math.Floor(Math.Log10(f)));
			var frac = f / logbase;
			frac = Math.Floor(frac);
			return (float) (logbase * frac);
		}
		public static string SIFormat(float f) {
			if (f >= 1000000000) return String.Format("{0}G", Math.Round(f / 1000000000, 2));
			if (f >= 1000000) return String.Format("{0}M", Math.Round(f / 1000000, 2));
			if (f >= 1000) return String.Format("{0}k", Math.Round(f / 1000, 2));
			if (f >= 1) return String.Format("{0}", Math.Round(f, 2));
			if (f >= 0.0001) return String.Format("{0}m", Math.Round(f * 1000, 2));
			if (f >= 0.0000001) return String.Format("{0}n", Math.Round(f * 1000000, 2));
			// give up
			return String.Format("{0}", f);
		}
		public static void GetEngineActions(IMyTerminalBlock block, List<IMyTerminalAction> actions) {
			if (!BlockIsEngine(block)) {
				actions.Remove(lowerHeightAction);
				actions.Remove(raiseHeightAction);
				actions.Remove(turnOnAction);
				actions.Remove(turnOffAction);
				actions.Remove(turnOnOffAction);
			}
		}
		public static void LowerHeightAction(IMyTerminalBlock block) {
			float height = (float) SettingsStore.Get(block, "height_offset", 1.25f);
			height = Math.Max(0.1f, (float) Math.Round(height - 0.1f, 1));
			SettingsStore.Set(block, "height_offset", height);
		}
		public static void RaiseHeightAction(IMyTerminalBlock block) {
			float height = (float) SettingsStore.Get(block, "height_offset", 1.25f);
			height = Math.Min(2.5f, (float) Math.Round(height + 0.1f, 1));
			SettingsStore.Set(block, "height_offset", height);
		}
		public static void TurnOnAction(IMyTerminalBlock block) {
			SettingsStore.Set(block, "power_on", true);
		}
		public static void TurnOffAction(IMyTerminalBlock block) {
			SettingsStore.Set(block, "power_on", false);
		}
		public static void TurnOnOffAction(IMyTerminalBlock block) {
			SettingsStore.Set(block, "power_on", !SettingsStore.Get(block, "power_on", true));
		}
		public static void OnOffWriter(IMyTerminalBlock block, StringBuilder builder) {
			builder.Clear();
			builder.Append(((bool) SettingsStore.Get(block, "power_on", true))?"On":"Off");
		}
		
		public static void InitLate() {
			initialized = true;
			
            powerSwitch = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("HoverRail_OnOff");
            powerSwitch.Title   = MyStringId.GetOrCompute("Maglev Engine");
			powerSwitch.Tooltip = MyStringId.GetOrCompute("Enable to apply force to stick to the track.");
            powerSwitch.Getter  = b => (bool) SettingsStore.Get(b, "power_on", true);
            powerSwitch.Setter  = (b, v) => SettingsStore.Set(b, "power_on", v);
			powerSwitch.SupportsMultipleBlocks = true;
			powerSwitch.OnText  = MyStringId.GetOrCompute("On");
			powerSwitch.OffText = MyStringId.GetOrCompute("Off");
			powerSwitch.Visible = BlockIsEngine;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(powerSwitch);
			
			forceSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>( "HoverRail_ForceLimit" );
			forceSlider.Title   = MyStringId.GetOrCompute("Force Limit");
			forceSlider.Tooltip = MyStringId.GetOrCompute("The amount of force applied to align this motor with the track.");
			forceSlider.SetLogLimits(10000.0f, 50000000.0f);
			forceSlider.SupportsMultipleBlocks = true;
			forceSlider.Getter  = b => (float) SettingsStore.Get(b, "force_slider", 100000.0f);
			forceSlider.Setter  = (b, v) => SettingsStore.Set(b, "force_slider", (float) LogRound(v));
			forceSlider.Writer  = (b, result) => result.Append(String.Format("{0}N", SIFormat((float) SettingsStore.Get(b, "force_slider", 100000.0f))));
			forceSlider.Visible = BlockIsEngine;
			MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(forceSlider);
			
			heightSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>( "HoverRail_HeightOffset" );
			heightSlider.Title   = MyStringId.GetOrCompute("Height Offset");
			heightSlider.Tooltip = MyStringId.GetOrCompute("The height we float above the track.");
			heightSlider.SetLimits(0.1f, 2.5f);
			heightSlider.SupportsMultipleBlocks = true;
			heightSlider.Getter  = b => (float) SettingsStore.Get(b, "height_offset", 1.25f);
			heightSlider.Setter  = (b, v) => SettingsStore.Set(b, "height_offset", (float) Math.Round(v, 1));
			heightSlider.Writer  = (b, result) => result.Append(String.Format("{0}m", (float) SettingsStore.Get(b, "height_offset", 1.25f)));
			heightSlider.Visible = BlockIsEngine;
			MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(heightSlider);
			
			lowerHeightAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("HoverRailEngine_LowerHeight0.1");
			lowerHeightAction.Name = new StringBuilder("Lower Height");
			lowerHeightAction.Action = LowerHeightAction;
			lowerHeightAction.Writer = (block, builder) => {
				builder.Clear();
				builder.Append(String.Format("{0} -", (float) SettingsStore.Get(block, "height_offset", 1.25f)));
			};
			MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(lowerHeightAction);
			
			raiseHeightAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("HoverRailEngine_RaiseHeight0.1");
			raiseHeightAction.Name = new StringBuilder("Raise Height");
			raiseHeightAction.Action = RaiseHeightAction;
			raiseHeightAction.Writer = (block, builder) => {
				builder.Clear();
				builder.Append(String.Format("{0} +", (float) SettingsStore.Get(block, "height_offset", 1.25f)));
			};
			MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(raiseHeightAction);
			
			turnOnAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("HoverRailEngine_On");
			turnOnAction.Name = new StringBuilder("Power On");
			turnOnAction.Action = TurnOnAction;
			turnOnAction.Writer = OnOffWriter;
			MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(turnOnAction);
			
			turnOffAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("HoverRailEngine_Off");
			turnOffAction.Name = new StringBuilder("Power Off");
			turnOffAction.Action = TurnOffAction;
			turnOffAction.Writer = OnOffWriter;
			MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(turnOffAction);
			
			turnOnOffAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("HoverRailEngine_OnOff");
			turnOnOffAction.Name = new StringBuilder("Power On/Off");
			turnOnOffAction.Action = TurnOnOffAction;
			turnOnOffAction.Writer = OnOffWriter;
			MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(turnOnOffAction);
			
			MyAPIGateway.TerminalControls.CustomActionGetter += GetEngineActions;
		}
	}
}
