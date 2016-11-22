using System;
using System.Text;
using System.Collections.Generic;
using Sandbox;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Common;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.Components;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace HoverRail {
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), "HoverRail_Junction_Left_10x-12x_Large")]
	public class HoverRailJunction_12x_Left : MyGameLogicComponent {
		Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder = null;
		bool block_initialized = false;
		MatrixD left_initial, right_initial;
        public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder) {
			this.objectBuilder = objectBuilder;
			// TODO freeze junctions when they're not moving
			Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
		}
		public void InitLate() {
			left_initial = Entity.GetSubpart("swivel_left").PositionComp.LocalMatrix;
			right_initial = Entity.GetSubpart("swivel_right").PositionComp.LocalMatrix;
			block_initialized = true;
			if (!JunctionUI.initialized) JunctionUI.InitLate();
		}
		public override void UpdateBeforeSimulation() {
			base.UpdateBeforeSimulation();
			if (!block_initialized) InitLate();
		}
		public override void UpdateAfterSimulation() {
			base.UpdateAfterSimulation();
			if (!(Entity as IMyCubeBlock).IsFunctional) return;
			
			var angle = ((bool) SettingsStore.Get(Entity, "junction_turn", false))?8:0;
			
			var left = Entity.GetSubpart("swivel_left");
			left.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * left_initial;
			var right = Entity.GetSubpart("swivel_right");
			right.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * right_initial;
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false) {
			return objectBuilder;
		}
	}
	
	class Junction_12x_Left_RailGuide : RailGuide {
		MatrixD junction_to_straight_right_short, straight_right_short_to_junction;
		MatrixD junction_to_straight_left_short, straight_left_short_to_junction;
		MatrixD junction_to_straight_right_long, straight_right_long_to_junction;
		MatrixD junction_to_straight_left_long, straight_left_long_to_junction;
		float last_angle = 0.0f;
		public Junction_12x_Left_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) {
			junction_to_straight_right_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * 5.5);
			junction_to_straight_left_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * 3.5);
			junction_to_straight_right_long = MatrixD.CreateTranslation(2.5 * 2.5, 0.0, 2.5 * 5.5);
			junction_to_straight_left_long = MatrixD.CreateTranslation(2.5 * 2.0, 0.0, 2.5 * 3.5);
			straight_right_short_to_junction = MatrixD.Invert(junction_to_straight_right_short);
			straight_left_short_to_junction = MatrixD.Invert(junction_to_straight_left_short);
			straight_right_long_to_junction = MatrixD.Invert(junction_to_straight_right_long);
			straight_left_long_to_junction = MatrixD.Invert(junction_to_straight_left_long);
		}
		// pick between curved guide and straight guide
		// the "danger hole" is the part of the rail where curved and straight cross
		// around that point, we don't apply any guide (guide=pos) and rely on the other rail to hold us steady.
		public static void pick_appropriate_guides(Vector3D pos, Vector3D dangerhole,
		                                           Vector3D guide1, float weight1,
												   Vector3D guide2, float weight2,
												   ref Vector3D guide, ref float weight
		) {
			if (weight1 == 0) {
				guide += guide2;
				weight += weight2;
				return;
			}
			if (weight2 == 0) {
				guide += guide1;
				weight += weight1;
				return;
			}
			guide1 /= weight1;
			guide2 /= weight2;
			var dist1 = (float) (pos - guide1).Length();
			var dist2 = (float) (pos - guide2).Length();
			var fac = dist1 / (dist1 + dist2);
			// MyLog.Default.WriteLine(String.Format("interpolating {0}, {1} around {2}: fac {3}", guide1, guide2, pos, fac));
			float mix_weight = weight1 * (1 - fac) + weight2 * fac;
			Vector3D mix_guide = guide1 * (1 - fac) + guide2 * fac;
			
			var dangerdir = pos - dangerhole;
			dangerdir.Y = 0; // only look at planar distance
			var dangerdist = dangerdir.Length(); // distance from dangerhole
			var danger = (float) Math.Pow(Math.Max(0, 1 - dangerdist / 3.5), 0.01); // bit over two block radius, to be safe
			// MyLog.Default.WriteLine(String.Format("dangerdist {0}, danger {1}", dangerdist, danger));
			var only_y_correction = new Vector3D(pos.X, mix_guide.Y, pos.Z);
			
			mix_weight = mix_weight * (1 - danger) + 1 * danger;
			mix_guide = mix_guide * (1 - danger) + only_y_correction * danger;
			
			guide += mix_guide * mix_weight;
			weight += mix_weight;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			// MyLog.Default.WriteLine(String.Format("local coord is {0}", localCoords));
			bool tracking = false;
			
			// dividing line between swivel and turn/straight (determined in blender)
			var div_left = new Vector3D(6, 0, -8.5);
			var div_right = new Vector3D(3.5, 0, -13);
			var tangent = div_right - div_left;
			var normal = new Vector3D(tangent.Z, 0, -tangent.X);
			// determine on which side of [div_left - div_right] we are
			var right_side = ((localCoords - div_left) * new Vector3D(1, 0, 1)).Dot(normal) > 0;
			if (right_side) {
				Vector3D curve_guide = new Vector3D(); float curve_weight = 0.0f;
				tracking |= Curve90_10x_12x_RailGuide.curved_guidance(
					localCoords,
					this.cubeBlock.WorldMatrix,
					false,
					ref curve_guide, ref curve_weight, height
				);
				Vector3D straight_guide = new Vector3D(); float straight_weight = 0.0f;
				tracking |= StraightRailGuide.straight_guidance(
					7*1.25f,
					straight_right_long_to_junction * this.cubeBlock.WorldMatrix,
					Vector3D.Transform(localCoords, junction_to_straight_right_long),
					ref straight_guide, ref straight_weight, height
				);
				tracking |= StraightRailGuide.straight_guidance(
					8*1.25f,
					straight_left_long_to_junction * this.cubeBlock.WorldMatrix,
					Vector3D.Transform(localCoords, junction_to_straight_left_long),
					ref straight_guide, ref straight_weight, height
				);
				pick_appropriate_guides(
					pos, Vector3D.Transform(new Vector3D(-4, 0, -8.75), this.cubeBlock.WorldMatrix), // see blender
					curve_guide, curve_weight,
					straight_guide, straight_weight,
					ref guide, ref weight
				);
			}
			
			// from swivel space into parent local space, recentering the rail piece for straight_guidance
			var swivel_right_mat = MatrixD.CreateTranslation(1.25 * 4, 0, 0) * this.cubeBlock.GetSubpart("swivel_right").PositionComp.LocalMatrix;
			var swivel_left_mat = MatrixD.CreateTranslation(1.25 * 3, 0, 0) * this.cubeBlock.GetSubpart("swivel_left").PositionComp.LocalMatrix;
			
			var right_world_mat = swivel_right_mat * this.cubeBlock.WorldMatrix;
			var left_world_mat = swivel_left_mat * this.cubeBlock.WorldMatrix;
			
			tracking |= StraightRailGuide.straight_guidance(
				4*1.25f,
				right_world_mat,
				Vector3D.Transform(pos, MatrixD.Invert(right_world_mat)),
				// subpart rails are at 0, but straight_guidance thinks they are at -1.25
				// so to float height above them, float height + 1.25 above where guidance thinks they are.
				ref guide, ref weight, height + 1.25f
			);

			tracking |= StraightRailGuide.straight_guidance(
				3*1.25f,
				left_world_mat,
				Vector3D.Transform(pos, MatrixD.Invert(left_world_mat)),
				ref guide, ref weight, height + 1.25f
			);

			tracking |= StraightRailGuide.straight_guidance(
				1.25f,
				straight_right_short_to_junction * this.cubeBlock.WorldMatrix,
				Vector3D.Transform(localCoords, junction_to_straight_right_short),
				ref guide, ref weight, height
			);
			tracking |= StraightRailGuide.straight_guidance(
				1.25f,
				straight_left_short_to_junction * this.cubeBlock.WorldMatrix,
				Vector3D.Transform(localCoords, junction_to_straight_left_short),
				ref guide, ref weight, height
			);
			return tracking;
		}
	}
	
	static class JunctionUI {
		public static bool initialized = false;
		public static IMyTerminalControlOnOffSwitch turnSwitch;
		
		public static void InitLate() {
			initialized = true;
			
            turnSwitch = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("Junction_TurnOnOff");
            turnSwitch.Title   = MyStringId.GetOrCompute("Junction Direction");
			turnSwitch.Tooltip = MyStringId.GetOrCompute("Which way should a train go?");
            turnSwitch.Getter  = b => (bool) SettingsStore.Get(b, "junction_turn", false);
            turnSwitch.Setter  = (b, v) => SettingsStore.Set(b, "junction_turn", v);
			turnSwitch.OnText  = MyStringId.GetOrCompute("Left");
			turnSwitch.OffText = MyStringId.GetOrCompute("Fwd");
			turnSwitch.Visible = BlockIsJunction;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(turnSwitch);
		}
		public static bool BlockIsJunction(IMyTerminalBlock block) {
			return block.BlockDefinition.SubtypeId == "HoverRail_Junction_Left_10x-12x_Large";
		}
	}
}
