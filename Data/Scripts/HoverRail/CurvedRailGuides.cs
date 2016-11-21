using System;
using VRageMath;

namespace HoverRail {
	class Curve90_10x_12x_RailGuide : RailGuide {
		public Curve90_10x_12x_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public static bool curved_guidance(Vector3D localCoords, MatrixD worldMat, bool lean, ref Vector3D guide, ref float weight, float height) {
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // TODO lower?
			// -15 .. 15
			var localCoords2 = new Vector3D(15, 0, 15) - localCoords; // 0 .. 30
			var angle = Math.Atan2(localCoords2.X, localCoords2.Z);
			if (angle < 0 || angle > Math.PI / 2) return false;
			var radius = Math.Sqrt(localCoords2.X * localCoords2.X + localCoords2.Z * localCoords2.Z);
			
			// var planarCoords = new Vector3D(15, 0, 15) - new Vector3D(radius, localCoords2.Y, 0.0);
			// MyLog.Default.WriteLine(String.Format("angle of {0} for {1} - {2}", angle, localCoords.ToString(), planarCoords.ToString()));
			
			// push the outer rail up a bit
			var leanHeight = 0.0;
			if (lean) leanHeight = Math.Sin(angle * 2); // 0 .. 1 .. 0
			// curve of rail matches approximately
			// x=23.75*cos(angle)*(1+pow(fabs(sin(angle*2)), 1.6)*0.063)
			// y=23.75*sin(angle)*(1+pow(fabs(sin(angle*2)), 1.6)*0.063)
			// (empirically determined in blender)
			var fudgeFactor = 1 + Math.Pow(Math.Abs(Math.Sin(angle * 2)), 1.6) * 0.063;
			var localRail1 = new Vector3D(15 - Math.Sin(angle) * 28.75 * fudgeFactor, height - 1.25 + leanHeight, 15 - Math.Cos(angle) * 28.75 * fudgeFactor);
			var localRail2 = new Vector3D(15 - Math.Sin(angle) * 23.75 * fudgeFactor, height - 1.25             , 15 - Math.Cos(angle) * 23.75 * fudgeFactor);
			// MyLog.Default.WriteLine(String.Format("rail1 = {0}, rail2 = {1}, local {2}", localRail1, localRail2, localCoords));
			
			var localDirToRail1 = localRail1 - localCoords;
			var localDirToRail2 = localRail2 - localCoords;
			var localDirToRail = Vector3D.Zero;
			var len1 = localDirToRail1.Length();
			var len2 = localDirToRail2.Length();
			double len;
			if (len1 < len2) { localDirToRail = localDirToRail1; len = len1; }
			else { localDirToRail = localDirToRail2; len = len2; }
			if (len > 1.75) return false;
			
			var worldRail = Vector3D.Transform(localCoords + localDirToRail, worldMat);
			// DebugDraw.Line(pos, worldRail, 0.15f);
			guide += worldRail;
			weight += 1;
			
			return true;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			return Curve90_10x_12x_RailGuide.curved_guidance(localCoords, this.cubeBlock.WorldMatrix, true, ref guide, ref weight, height);
		}
	}
}
