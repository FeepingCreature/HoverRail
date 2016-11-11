using System;
using VRageMath;

static class StraightRailConstants {
	// engine still catches this far off the block. important as it allows us to transition betweeen rails safely
	// bit less than half a block, so we can bridge a 2/3-block gap safely
	public const float OVERHANG = 1.2f;
}

namespace HoverRail {
	abstract class StraightRailGuide : RailGuide {
		float halfsize;
		public StraightRailGuide(IMyCubeBlock cubeBlock, float halfsize) : base(cubeBlock) { this.halfsize = halfsize; }
		// also used in sloped rail
		public static bool straight_guidance(float halfsize, Vector3D pos, MatrixD cubeMatrix, Vector3D localCoords,
			ref Vector3D guide, ref float weight, float height
		) {
			localCoords.Y -= height - 1.25;
			var overhang = StraightRailConstants.OVERHANG;
			if (localCoords.X < -halfsize - overhang || localCoords.X > halfsize + overhang) return false; // outside the box
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // some leeway above TODO lower
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false; // outside the box
			var localDirToRail = new Vector3D(0, -localCoords.Y, -localCoords.Z);
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, cubeMatrix);
			// TODO compute guide rail pos directly
			float myWeight;
			const float underhang = 1.5f; // start transition early
			const float HANG = StraightRailConstants.OVERHANG + underhang;
			float transitionStart = halfsize - underhang;
			if (localCoords.X < -transitionStart || localCoords.X > transitionStart) {
				// influence goes down to 0 
				myWeight = (float) ((HANG - (float) (Math.Abs(localCoords.X) - transitionStart)) / HANG);
			} else {
				myWeight = 1.0f;
			}
			weight += myWeight;
			guide += (pos + worldDirToRail) * myWeight;
			return true;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			return straight_guidance(halfsize, pos, this.cubeBlock.WorldMatrix, localCoords, ref guide, ref weight, height);
		}
	}
	class Straight1xRailGuide : StraightRailGuide {
		public Straight1xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock, 1.25f) { }
	}
	
	class Straight3xRailGuide : StraightRailGuide {
		public Straight3xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock, 3.75f) { }
	}
	
	class Straight10xRailGuide : StraightRailGuide {
		public Straight10xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock, 12.5f) { }
	}
	
	class Straight30xRailGuide : StraightRailGuide {
		public Straight30xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock, 37.5f) { }
	}
}
