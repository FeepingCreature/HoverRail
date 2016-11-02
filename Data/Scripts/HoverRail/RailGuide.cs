using Sandbox.Game.Entities;
using VRageMath;

namespace HoverRail {
	abstract class RailGuide {
		public IMyCubeBlock cubeBlock;
		public string subtypeId;
		public RailGuide(IMyCubeBlock cubeBlock) {
			this.subtypeId = cubeBlock.BlockDefinition.SubtypeId;
			this.cubeBlock = cubeBlock;
		}
		public void applyForces(IMyEntity entity, Vector3D entforce) {
			IMyCubeGrid entgrid = null;
			var entblock = entity as IMyCubeBlock;
			if (entblock != null) entgrid = entblock.CubeGrid;
			
			// action
			if (entgrid != null && entgrid.Physics != null && !entgrid.IsStatic) {
				// MyLog.Default.WriteLine(String.Format("add force {0} at {1}", entforce, entity.WorldMatrix.Translation));
				entgrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, entforce, entity.WorldMatrix.Translation, Vector3.Zero);
			} else {
				// MyLog.Default.WriteLine(String.Format("don't add force - {0}", entgrid != null));
			}
			
			// reaction
			var cubeGrid = this.cubeBlock.CubeGrid;
			if (cubeGrid.Physics != null && !cubeGrid.IsStatic) {
				cubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -entforce, this.cubeBlock.WorldMatrix.Translation, Vector3.Zero);
			}
		}
		public virtual bool getGuidance(Vector3D pos, ref Vector3D direction) {
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
			return null;
		}
	}
}
