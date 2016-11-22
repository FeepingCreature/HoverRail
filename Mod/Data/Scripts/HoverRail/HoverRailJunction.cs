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
	public class HoverRailJunction : MyGameLogicComponent {
		Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder = null;
		bool block_initialized = false;
		protected MatrixD short_initial, long_initial;
		protected string swivel_short, swivel_long;
        public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder) {
			this.objectBuilder = objectBuilder;
			// TODO freeze junctions when they're not moving
			Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
		}
		public void InitLate() {
			short_initial = Entity.GetSubpart(swivel_short).PositionComp.LocalMatrix;
			long_initial = Entity.GetSubpart(swivel_long).PositionComp.LocalMatrix;
			block_initialized = true;
			if (!JunctionUI.initialized) JunctionUI.InitLate();
		}
		public override void UpdateBeforeSimulation() {
			base.UpdateBeforeSimulation();
			if (!block_initialized) InitLate();
		}
		public override void UpdateAfterSimulation() {
			base.UpdateAfterSimulation();
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false) {
			return objectBuilder;
		}
	}
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), "HoverRail_Junction_Left_10x-12x_Large")]
	public class HoverRailJunction_12x_Left : HoverRailJunction {
		public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);
			swivel_short = "jl_swivel_left";
			swivel_long = "jl_swivel_right";
		}
		public override void UpdateAfterSimulation() {
			base.UpdateAfterSimulation();
			if (!(Entity as IMyCubeBlock).IsFunctional) return;
			
			var angle = ((bool) SettingsStore.Get(Entity, "junction_turn", false))?8:0;
			
			var sw_short = Entity.GetSubpart(swivel_short);
			sw_short.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * short_initial;
			var sw_long = Entity.GetSubpart(swivel_long);
			sw_long.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * long_initial;
		}
	}
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), "HoverRail_Junction_Right_10x-12x_Large")]
	public class HoverRailJunction_12x_Right : HoverRailJunction {
		public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);
			swivel_short = "jr_swivel_right";
			swivel_long = "jr_swivel_left";
		}
		public override void UpdateAfterSimulation() {
			base.UpdateAfterSimulation();
			if (!(Entity as IMyCubeBlock).IsFunctional) return;
			
			var angle = ((bool) SettingsStore.Get(Entity, "junction_turn", false))?-8:0;
			
			var sw_short = Entity.GetSubpart(swivel_short);
			sw_short.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * short_initial;
			var sw_long = Entity.GetSubpart(swivel_long);
			sw_long.PositionComp.LocalMatrix = Matrix.CreateRotationY((float) (angle * Math.PI / 180.0)) * long_initial;
		}
	}
	
	// generic top class for junction left/right
	abstract class Junction_12x_RailGuide : RailGuide {
		public Junction_12x_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		// subspace matrices for the four straight rail pieces
		// "short" being the bit before the swivels, "long" being the bit after
		protected MatrixD junction_to_straight_inner_short, straight_inner_short_to_junction;
		protected MatrixD junction_to_straight_outer_short, straight_outer_short_to_junction;
		protected MatrixD junction_to_straight_inner_long, straight_inner_long_to_junction;
		protected MatrixD junction_to_straight_outer_long, straight_outer_long_to_junction;
		// the "danger hole" is the part of the rail where curved and straight cross
		// around that point, we don't apply any guide except Y (guide=pos) and rely on the other rail to hold us steady.
		protected Vector3D dangerhole;
		// form a line separating the "straight/left turns area" from the "swivel area"
		protected Vector3D divider_pt1, divider_pt2;
		// names of the swivel subparts
		protected string swivel_short, swivel_long;
		// the right junction uses a z-flipped curve piece
		protected bool flip_curve_z = false;
		// pick between curved guide and straight guide
		public static void pick_appropriate_guides(Vector3D pos, Vector3D dangerhole,
		                                           Vector3D guide1, float weight1,
												   Vector3D guide2, float weight2,
												   ref Vector3D guide, ref float weight
		) {
			if (weight1 == 0 && weight2 == 0) return;
			
			var dangerdir = pos - dangerhole;
			dangerdir.Y = 0; // only look at planar distance
			var dangerdist = dangerdir.Length(); // distance from dangerhole
			var danger = (float) Math.Pow(Math.Max(0, 1 - dangerdist / 3.5), 0.1); // bit over two block radius, to be safe
			// MyLog.Default.WriteLine(String.Format("dangerdist {0}, danger {1}", dangerdist, danger));
			var only_y_correction = new Vector3D(pos.X, 0 /* fill in once clear which guides are available */, pos.Z);
			
			if (weight1 == 0) {
				// MyLog.Default.WriteLine("only guide 2!");
				// TODO I do not know if it's correct to do linear interpolation on premultiplied vectors.
				only_y_correction.Y = guide2.Y / weight2;
				guide += guide2 * (1 - danger) + only_y_correction * danger;
				weight += weight2 * (1 - danger) + danger;
				return;
			}
			if (weight2 == 0) {
				// MyLog.Default.WriteLine("only guide 1!");
				only_y_correction.Y = guide1.Y / weight1;
				guide += guide1 * (1 - danger) + only_y_correction * danger;
				weight += weight1 * (1 - danger) + danger;
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
			
			only_y_correction.Y = mix_guide.Y;
			mix_guide *= mix_weight;
			
			// see above re safe?
			mix_guide = mix_guide * (1 - danger) + only_y_correction * danger;
			mix_weight = mix_weight * (1 - danger) + danger;
			
			guide += mix_guide;
			weight += mix_weight;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			// MyLog.Default.WriteLine(String.Format("local coord is {0} [{1}]", localCoords, flip_curve_z));
			bool tracking = false;
			
			var tangent = divider_pt2 - divider_pt1;
			var normal = new Vector3D(tangent.Z, 0, -tangent.X); // 90° rotated
			// determine on which side of the divider we are
			var right_side = ((localCoords - divider_pt1) * new Vector3D(1, 0, 1)).Dot(normal) > 0;
			if (right_side) {
				Vector3D curve_guide = new Vector3D(); float curve_weight = 0.0f;
				var curveCoords = localCoords;
				var curveWorldMat = this.cubeBlock.WorldMatrix;
				var invZ = new Vector3D(1, 1, -1);
				if (flip_curve_z) {
					curveCoords *= invZ;
					MatrixD.Rescale(ref curveWorldMat, ref /* WHY */ invZ);
				}
				Curve90_10x_12x_RailGuide.curved_guidance(
					curveCoords,
					curveWorldMat,
					false,
					ref curve_guide, ref curve_weight, height
				);
				Vector3D straight_guide = new Vector3D(); float straight_weight = 0.0f;
				StraightRailGuide.straight_guidance(
					7*1.25f,
					straight_outer_long_to_junction * this.cubeBlock.WorldMatrix,
					Vector3D.Transform(localCoords, junction_to_straight_outer_long),
					ref straight_guide, ref straight_weight, height
				);
				StraightRailGuide.straight_guidance(
					8*1.25f,
					straight_inner_long_to_junction * this.cubeBlock.WorldMatrix,
					Vector3D.Transform(localCoords, junction_to_straight_inner_long),
					ref straight_guide, ref straight_weight, height
				);
				float picked_weight = 0;
				pick_appropriate_guides(
					pos, Vector3D.Transform(dangerhole, this.cubeBlock.WorldMatrix),
					curve_guide, curve_weight,
					straight_guide, straight_weight,
					ref guide, ref picked_weight
				);
				weight += picked_weight;
				tracking |= picked_weight > 0;
			}
			
			// from swivel space into parent local space, recentering the rail piece for straight_guidance
			var swivel_long_mat = MatrixD.CreateTranslation(1.25 * 4, 0, 0) * this.cubeBlock.GetSubpart(swivel_long).PositionComp.LocalMatrix;
			var swivel_short_mat = MatrixD.CreateTranslation(1.25 * 3, 0, 0) * this.cubeBlock.GetSubpart(swivel_short).PositionComp.LocalMatrix;
			
			var sw_long_world_mat = swivel_long_mat * this.cubeBlock.WorldMatrix;
			var sw_short_world_mat = swivel_short_mat * this.cubeBlock.WorldMatrix;
			
			tracking |= StraightRailGuide.straight_guidance(
				4*1.25f,
				sw_long_world_mat,
				Vector3D.Transform(pos, MatrixD.Invert(sw_long_world_mat)),
				// subpart rails are at 0, but straight_guidance thinks they are at -1.25
				// so to float height above them, float height + 1.25 above where guidance thinks they are.
				ref guide, ref weight, height + 1.25f
			);

			tracking |= StraightRailGuide.straight_guidance(
				3*1.25f,
				sw_short_world_mat,
				Vector3D.Transform(pos, MatrixD.Invert(sw_short_world_mat)),
				ref guide, ref weight, height + 1.25f
			);

			tracking |= StraightRailGuide.straight_guidance(
				1.25f,
				straight_outer_short_to_junction * this.cubeBlock.WorldMatrix,
				Vector3D.Transform(localCoords, junction_to_straight_outer_short),
				ref guide, ref weight, height
			);
			tracking |= StraightRailGuide.straight_guidance(
				1.25f,
				straight_inner_short_to_junction * this.cubeBlock.WorldMatrix,
				Vector3D.Transform(localCoords, junction_to_straight_inner_short),
				ref guide, ref weight, height
			);
			return tracking;
		}
	}
	
	class Junction_12x_Left_RailGuide : Junction_12x_RailGuide {
		public Junction_12x_Left_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) {
			junction_to_straight_inner_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * 3.5);
			junction_to_straight_outer_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * 5.5);
			junction_to_straight_inner_long = MatrixD.CreateTranslation(2.5 * 2.0, 0.0, 2.5 * 3.5);
			junction_to_straight_outer_long = MatrixD.CreateTranslation(2.5 * 2.5, 0.0, 2.5 * 5.5);
			straight_inner_short_to_junction = MatrixD.Invert(junction_to_straight_inner_short);
			straight_outer_short_to_junction = MatrixD.Invert(junction_to_straight_outer_short);
			straight_inner_long_to_junction = MatrixD.Invert(junction_to_straight_inner_long);
			straight_outer_long_to_junction = MatrixD.Invert(junction_to_straight_outer_long);
			swivel_short = "jl_swivel_left";
			swivel_long = "jl_swivel_right";
			// see blender
			dangerhole = new Vector3D(-4, 0, -8.75);
			divider_pt1 = new Vector3D(6, 0, -8.5);
			divider_pt2 = new Vector3D(3.5, 0, -13);
			flip_curve_z = false;
		}
	}
	
	class Junction_12x_Right_RailGuide : Junction_12x_RailGuide {
		public Junction_12x_Right_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) {
			junction_to_straight_inner_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * -3.5);
			junction_to_straight_outer_short = MatrixD.CreateTranslation(2.5 * -5.5, 0.0, 2.5 * -5.5);
			junction_to_straight_inner_long = MatrixD.CreateTranslation(2.5 * 2.0, 0.0, 2.5 * -3.5);
			junction_to_straight_outer_long = MatrixD.CreateTranslation(2.5 * 2.5, 0.0, 2.5 * -5.5);
			straight_inner_short_to_junction = MatrixD.Invert(junction_to_straight_inner_short);
			straight_outer_short_to_junction = MatrixD.Invert(junction_to_straight_outer_short);
			straight_inner_long_to_junction = MatrixD.Invert(junction_to_straight_inner_long);
			straight_outer_long_to_junction = MatrixD.Invert(junction_to_straight_outer_long);
			swivel_short = "jr_swivel_right";
			swivel_long = "jr_swivel_left";
			// see blender
			dangerhole = new Vector3D(-4, 0, 8.75);
			divider_pt1 = new Vector3D(3.5, 0, 13);
			divider_pt2 = new Vector3D(6, 0, 8.5);
			flip_curve_z = true;
		}
	}
	
	static class JunctionUI {
		public static bool initialized = false;
		public static IMyTerminalControlOnOffSwitch turnLeftSwitch, turnRightSwitch;
		
		public static void InitLate() {
			initialized = true;
			
            turnLeftSwitch = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("LeftJunction_TurnOnOff");
            turnLeftSwitch.Title   = MyStringId.GetOrCompute("Junction Direction");
			turnLeftSwitch.Tooltip = MyStringId.GetOrCompute("Which way should a train go?");
            turnLeftSwitch.Getter  = b => (bool) SettingsStore.Get(b, "junction_turn", false);
            turnLeftSwitch.Setter  = (b, v) => SettingsStore.Set(b, "junction_turn", v);
			turnLeftSwitch.OnText  = MyStringId.GetOrCompute("Left");
			turnLeftSwitch.OffText = MyStringId.GetOrCompute("Fwd");
			turnLeftSwitch.Visible = BlockIsLeftJunction;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(turnLeftSwitch);
			
            turnRightSwitch = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("RightJunction_TurnOnOff");
            turnRightSwitch.Title   = MyStringId.GetOrCompute("Junction Direction");
			turnRightSwitch.Tooltip = MyStringId.GetOrCompute("Which way should a train go?");
			// meaning flipped relative to switch, so that we can
			// swap the labels and have [Fwd] [Right], which is more pleasing
            turnRightSwitch.Getter  = b => !(bool) SettingsStore.Get(b, "junction_turn", false);
            turnRightSwitch.Setter  = (b, v) => SettingsStore.Set(b, "junction_turn", !v);
			turnRightSwitch.OnText  = MyStringId.GetOrCompute("Fwd");
			turnRightSwitch.OffText = MyStringId.GetOrCompute("Right");
			turnRightSwitch.Visible = BlockIsRightJunction;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(turnRightSwitch);
		}
		public static bool BlockIsLeftJunction(IMyTerminalBlock block) {
			return block.BlockDefinition.SubtypeId == "HoverRail_Junction_Left_10x-12x_Large";
		}
		public static bool BlockIsRightJunction(IMyTerminalBlock block) {
			return block.BlockDefinition.SubtypeId == "HoverRail_Junction_Right_10x-12x_Large";
		}
	}
}
