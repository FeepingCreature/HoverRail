using System;
using VRageMath;
using VRage.Utils;

namespace HoverRail {
	static class MinSearch {
		public static bool find_smallest(ref double where, Func<double, double> fun, double initial_estimate, double from, double to, int iters) {
			Func<double, double> dfun = f => (fun(f + 0.001) - fun(f - 0.001)) * (1.0 / 0.002);
			double left = dfun(from), right = dfun(to);
			if (left < 0 && right < 0 || left > 0 && right > 0) {
				// MyLog.Default.WriteLine(String.Format("WARN: subdivision method premise failed, please report this"));
				// return initial_estimate;
				return false;
			}
			for (int i = 0; i < iters; i++) {
				double half = (from + to) * 0.5;
				double val = dfun(half);
				if (left < 0 && val < 0) {
					from = half;
					left = val;
				} else {
					to = half;
					right = val;
				}
			}
			where = (from + to) * 0.5;
			return true;
		}
	}
	class Sloped5xRailGuide : RailGuide {
		MatrixD adjustMatrix, unadjustMatrix; 
		public Sloped5xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) {
			// it's a slope, so it's rotated around the center
			var angle = Math.Acos(5 / Math.Sqrt(5*5 + 1*1));
			// diagonal rail is half a block up from straight rail
			this.adjustMatrix = MatrixD.CreateTranslation(0, -1.25, 0) * MatrixD.CreateRotationZ(-angle);
			this.unadjustMatrix = MatrixD.Invert(this.adjustMatrix);
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			// size: 5x2x1, meaning 12.5 x 5 x 2.5, slope of -1/5
			// rail begins at x=-6.25 y=1.25 and goes to x=6.25 y=-1.25
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			var unrotatedCoords = Vector3D.Transform(localCoords, ref this.adjustMatrix);
			// MyLog.Default.WriteLine(String.Format("angle of {0} turns {1} to {2}", Math.Acos(5 / Math.Sqrt(5*5 + 1*1)), localCoords, unrotatedCoords));
			var length = (float) Math.Sqrt(5*5 + 1*1) * 2.5f;
			
			return StraightRailGuide.straight_guidance(length, this.unadjustMatrix * this.cubeBlock.WorldMatrix, unrotatedCoords,
				ref guide, ref weight, height);
		}
	}
	
	class SlopeTop5xRailGuide : RailGuide {
		public SlopeTop5xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		// rail is -0.008 x^2 - (12.5*0.008) x - 6.25^2 * 0.008
		public static double guidefn(double x) {
			return -0.008 * x * x - (12.5*0.008) * x - 6.25*6.25*0.008;
		}
		public static double distance(double u, double v, double x) {
			var y = guidefn(x);
			double dx = u - x, dy = v - y;
			return dx * dx + dy * dy;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			// size: 5x2x1, meaning 12.5 x 5 x 2.5
			// approximated by y = -(x+6.25)^2*0.008, see http://tinyurl.com/gnm5akr
			// for X flip, see below
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			localCoords.X = -localCoords.X; // just mirror, todo redo the equation
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false;
			localCoords.Y -= height; // guide height above rail -- so we track the actual slope
			// closest point to a poly is not entirely trivial and I cba solving it manually, so resort to the laziest approach possible: BINARY SEARCH.
			double x = 0.0;
			bool success = MinSearch.find_smallest(ref x, xpar => distance(localCoords.X, localCoords.Y, xpar), localCoords.X, -6.25, 6.25, 10);
			// MyLog.Default.WriteLine(String.Format("success {0} finding x {1} which has guidefn {2} at {3}", success, x, guidefn(x), localCoords));
			if (!success) return false;
			
			var localGuidepoint = new Vector3D(-x, guidefn(x) + height, 0);
			var worldGuidepoint = Vector3D.Transform(localGuidepoint, this.cubeBlock.WorldMatrix);
			guide += worldGuidepoint;
			weight += 1;
			return true;
		}
	}
	
	class SlopeBottom5xRailGuide : RailGuide {
		public SlopeBottom5xRailGuide(IMyCubeBlock cubeBlock) : base(cubeBlock) { }
		// rail is 0.008 x^2 + (12.5*0.008) x + 6.25^2 * 0.008
		public static double railfn(double x) {
			return 0.008 * x * x + (12.5*0.008) * x + 6.25*6.25*0.008 - 2.5;
		}
		public static double distance(double u, double v, double x) {
			var y = railfn(x);
			double dx = u - x, dy = v - y;
			return dx * dx + dy * dy;
		}
		public override bool getGuidance(Vector3D pos, ref Vector3D guide, ref float weight, float height) {
			if (!base.getGuidance(pos, ref guide, ref weight, height)) return false;
			// size: 5x1x1, meaning 12.5 x 2.5 x 2.5
			// approximated by y = (x+6.25)^2*0.008-2.5, see http://tinyurl.com/j9q6fc4
			// except with flipped x because whyy
			
			var localCoords = Vector3D.Transform(pos, this.cubeBlock.WorldMatrixNormalizedInv);
			localCoords.X = -localCoords.X; // just mirror, todo redo the equation
			if (localCoords.Z < -1.25 || localCoords.Z > 1.25) return false;
			// "rail coords"
			localCoords.Y -= height; // see above
			double x = 0.0;
			bool success = MinSearch.find_smallest(ref x, xpar => distance(localCoords.X, localCoords.Y, xpar), localCoords.X, -6.25, 6.25, 10);
			if (!success) return false;
			
			var localGuidepoint = new Vector3D(-x, railfn(x) + height, 0);
			var worldGuidepoint = Vector3D.Transform(localGuidepoint, this.cubeBlock.WorldMatrix);
			guide += worldGuidepoint;
			weight += 1;
			return true;
		}
	}
}
