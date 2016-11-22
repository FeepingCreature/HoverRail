using System;
using Sandbox.Game.Entities;
using VRage.Utils;
using VRageMath;

namespace HoverRail {
	abstract class RailGuide {
		public IMyCubeBlock cubeBlock;
		public string subtypeId;
		public RailGuide(IMyCubeBlock cubeBlock) {
			this.subtypeId = cubeBlock.BlockDefinition.SubtypeId;
			this.cubeBlock = cubeBlock;
		}
		public override int GetHashCode() {
			return (int) this.cubeBlock.EntityId;
		}
		public override bool Equals(object obj) {
			RailGuide rg = obj as RailGuide;
			return rg != null && rg.cubeBlock == cubeBlock;
		}
		public void applyForces(IMyEntity entity, Vector3D entforce) {
			IMyCubeGrid entgrid = null;
			var entblock = entity as IMyCubeBlock;
			if (entblock != null) entgrid = entblock.CubeGrid;
			
			var force_pos = entity.WorldMatrix.Translation; // center of engine
			
			// action
			if (entgrid != null && entgrid.Physics != null && !entgrid.IsStatic) {
				// MyLog.Default.WriteLine(String.Format("add force {0} at {1}", entforce, force_pos));
				entgrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, entforce, force_pos, Vector3.Zero);
			} else {
				// MyLog.Default.WriteLine(String.Format("don't add force - {0}", entgrid != null));
			}
			
			// reaction
			var cubeGrid = this.cubeBlock.CubeGrid;
			if (cubeGrid.Physics != null && !cubeGrid.IsStatic) {
				cubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -entforce, force_pos, Vector3.Zero);
			}
		}
		// note: new values must be *ADDED* to guide and weight!
		// height indicates the height that the guide rail should be above the track
		public virtual bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			return cubeBlock.IsFunctional;
		}
		public static RailGuide fromEntity(IMyEntity ent) {
			var cubeBlock = ent as IMyCubeBlock;
			if (cubeBlock == null) return null;
			var subtypeId = cubeBlock.BlockDefinition.SubtypeId;
			if (subtypeId == "HoverRail_Straight_x1_Large") {
				return new Straight1xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Straight_x3_Large") {
				return new Straight3xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Straight_x10_Large") {
				return new Straight10xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Straight_x30_Large") {
				return new Straight30xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Curved_90_10x-12x_Large") {
				return new Curve90_10x_12x_RailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Slope_Top_x5_Large") {
				return new SlopeTop5xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Slope_x5_Large") {
				return new Sloped5xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Slope_Bottom_x5_Large") {
				return new SlopeBottom5xRailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Junction_Left_10x-12x_Large") {
				return new Junction_12x_Left_RailGuide(cubeBlock);
			}
			if (subtypeId == "HoverRail_Junction_Right_10x-12x_Large") {
				return new Junction_12x_Right_RailGuide(cubeBlock);
			}
			return null;
		}
	}
}
