using System;
using VRageMath;

namespace HoverRail {
	class Straight1xRailGuide : RailGuide {
		public Straight1xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public override bool getGuidance(Vector3D pos, ref Vector3D direction) {
			if (!base.getGuidance(pos, ref direction)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			if (localCoords.X < -1.25 || localCoords.X > 1.25) return false; // outside the box
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // some leeway above TODO lower
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false; // outside the box
			var localDirToRail = new Vector3D(0, -localCoords.Y, -localCoords.Z);
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, this.cubeBlock.WorldMatrix);
			direction = worldDirToRail;
			return true;
		}
	}
	
	class Straight3xRailGuide : RailGuide {
		public Straight3xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public override bool getGuidance(Vector3D pos, ref Vector3D direction) {
			if (!base.getGuidance(pos, ref direction)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			if (localCoords.X < -3.75 || localCoords.X > 3.75) return false; // outside the box
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // some leeway above TODO lower
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false; // outside the box
			var localDirToRail = new Vector3D(0, -localCoords.Y, -localCoords.Z);
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, this.cubeBlock.WorldMatrix);
			direction = worldDirToRail;
			return true;
		}
	}
	
	class Straight10xRailGuide : RailGuide {
		public Straight10xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public override bool getGuidance(Vector3D pos, ref Vector3D direction) {
			if (!base.getGuidance(pos, ref direction)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			if (localCoords.X < -12.5 || localCoords.X > 12.5) return false; // outside the box
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // some leeway above TODO lower
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false; // outside the box
			var localDirToRail = new Vector3D(0, -localCoords.Y, -localCoords.Z);
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, this.cubeBlock.WorldMatrix);
			direction = worldDirToRail;
			return true;
		}
	}
	
	class Straight30xRailGuide : RailGuide {
		public Straight30xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public override bool getGuidance(Vector3D pos, ref Vector3D direction) {
			if (!base.getGuidance(pos, ref direction)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			if (localCoords.X < -37.5 || localCoords.X > 37.5) return false; // outside the box
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // some leeway above TODO lower
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false; // outside the box
			var localDirToRail = new Vector3D(0, -localCoords.Y, -localCoords.Z);
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, this.cubeBlock.WorldMatrix);
			direction = worldDirToRail;
			return true;
		}
	}
}
