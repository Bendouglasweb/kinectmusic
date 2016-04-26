using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
	class NoteSelector
	{
		private double[] xhistory;
		private double[] yhistory;
		private double[] velocity;
		private double[] direction;
		private double[] dirChange;
		private double[] choords;		// (x,y)
		private double velSum;
		private int bufferSize;
		private double velocitySensitivity;
		private double directionSensitivity;
		
		public NoteSelector(int bufferSizeSetting, double velocitySensitivitySetting, double directionSensitivitySetting) 
		{
			bufferSize = bufferSizeSetting;
			xhistory = new double[bufferSize];
			yhistory = new double[bufferSize];
			velocity = new double[bufferSize];
			direction = new double[bufferSize];
			dirChange = new double[bufferSize];
			velocitySensitivity = velocitySensitivitySetting;
			directionSensitivity = directionSensitivitySetting;
			choords = new double[2];
			initHistories();
		}
		private void initHistories() 
		{
			for (int i = 0; i < bufferSize; i++) {
				xhistory[i] = 0;
				yhistory[i] = 0;
				velocity[i] = 0;
				direction[i] = 0;
				dirChange[i] = 0;				
			}
			velSum = 0;
			choords[0] = 0;
			choords[1] = 0;
		}
		// (x1,y1) = Where are you are
		// (x2,y2) = Where you were
		private double degrees(double x1, double y1, double x2, double y2) {			
			
			double y = y1 - y2;
			double x = x1 - x2;
			double radians;
			if (y >= 0) {
				radians = Math.Atan2(y,x);
			}
			else {
				radians = Math.PI + (Math.PI-(-1)*Math.Atan2(y,x));
			}
			return radians;
		}
		
		// From d1 to d2
		private double degreeChange(double d2, double d1) {
			double diff = d2 - d1;			
			if (diff > (Math.PI)) {
				diff -= (diff-180)*2;
			}
			else if (diff < -Math.PI) {
				diff -= (diff+180)*2;
			}			
			return diff;
		}
		public double[] pushNote(double x_in, double y_in)
		{
			velSum -= velocity[bufferSize-1];
			
			for (int i = bufferSize-1; i > 0; i--) {
				xhistory[i] = xhistory[i-1];
				yhistory[i] = yhistory[i-1];
				velocity[i] = velocity[i-1];
				direction[i] = direction[i-1];
				dirChange[i] = dirChange[i-1];
			}
			
			xhistory[0] = x_in;
			yhistory[0] = y_in;
			velocity[0] = Math.Sqrt(Math.Pow((yhistory[0] - yhistory[1]),2) + Math.Pow((xhistory[0] - xhistory[1]),2));
			direction[0] = degrees(xhistory[0],yhistory[0],xhistory[1],yhistory[1]);
			dirChange[0] = degreeChange(direction[0],direction[1]);
			
			velSum += velocity[0];
					
			
			for (int i = bufferSize-1; i >= 0; i--) {
				Console.Write(xhistory[i] + " ");
				Console.Write(yhistory[i] + "   ");
				Console.Write(string.Format("{0:0.00}", velocity[i]) + " ");
				Console.Write(string.Format("{0:0.00}", direction[i]) + " ");
				Console.Write(string.Format("{0:0.00}", (velSum / bufferSize)) + " ");
				Console.Write(string.Format("{0:0.00}", dirChange[i]) + "\n");
				
			}
			
			
			if ((velSum / bufferSize) < velocitySensitivity) {
				choords[0] = xhistory[0];
				choords[1] = yhistory[0];
			}
			else if (dirChange[0] > directionSensitivity) {
				choords[0] = xhistory[0];
				choords[1] = yhistory[0];
			}
			return choords;
			
		}
	}
}
