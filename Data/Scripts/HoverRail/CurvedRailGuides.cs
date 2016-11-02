using System;
using VRageMath;

namespace HoverRail {
	class Curve90_10x_12x_RailGuide : RailGuide {
		public Curve90_10x_12x_RailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		public override bool getGuidance(Vector3D pos, ref Vector3D direction) {
			if (!base.getGuidance(pos, ref direction)) return false;
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			if (localCoords.Y < -1.25 || localCoords.Y > 2.60) return false; // TODO lower?
			// -15 .. 15
			var localCoords2 = new Vector3D(15, 0, 15) - localCoords; // 0 .. 30
			var angle = Math.Atan2(localCoords2.X, localCoords2.Z);
			if (angle < 0 || angle > Math.PI / 2) return false;
			var radius = Math.Sqrt(localCoords2.X * localCoords2.X + localCoords2.Z * localCoords2.Z);
			
			var planarCoords = new Vector3D(15, 0, 15) - new Vector3D(radius, localCoords2.Y, 0.0);
			// MyLog.Default.WriteLine(String.Format("angle of {0} for {1} - {2}", angle, localCoords.ToString(), planarCoords.ToString()));
			
			// push the outer rail up a bit
			var height = Math.Sin(angle * 2); // 0 .. 1 .. 0
			var rail1 = new Vector3D(15 - Math.Sin(angle) * 28.75, height, 15 - Math.Cos(angle) * 28.75);
			var rail2 = new Vector3D(15 - Math.Sin(angle) * 23.75, 0, 15 - Math.Cos(angle) * 23.75);
			// MyLog.Default.WriteLine(String.Format("rail1 = {0}, rail2 = {1}, local {2}", rail1.ToString(), rail2.ToString(), localCoords.ToString()));
			
			var localDirToRail1 = rail1 - localCoords;
			var localDirToRail2 = rail2 - localCoords;
			var localDirToRail = Vector3D.Zero;
			var len1 = localDirToRail1.Length();
			var len2 = localDirToRail2.Length();
			double len;
			if (len1 < len2) { localDirToRail = localDirToRail1; len = len1; }
			else { localDirToRail = localDirToRail2; len = len2; }
			if (len > 1.75) return false;
			
			var worldDirToRail = Vector3D.TransformNormal(localDirToRail, this.cubeBlock.WorldMatrix);
			DebugDraw.Line(pos, pos + worldDirToRail, 0.15f);
			direction = worldDirToRail;
			return true;
		}
	}
}
