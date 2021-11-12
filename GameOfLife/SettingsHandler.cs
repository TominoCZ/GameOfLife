using System;
using Newtonsoft.Json;
using OpenTK;
using System.IO;
using System.Threading;

namespace GameOfLife
{
	internal static class SettingsHandler
	{
		public static SettingsObject Settings { get; set; }

		public static void TryLoad(Window w)
		{
			Settings = new SettingsObject
			{
				VSync = true,
				FPS = 60,
				//CellsX = w.Width / 10,
				//CellsY = w.Height / 10,
				CellSize = 4,
				BlurFrames = 200,
				BottomValue = 20,
				FillScreens = true,
				Images = new[] { "image.png" }
			};

			try
			{
				if (File.Exists("GameOfLife.json"))
				{
					var json = File.ReadAllText("GameOfLife.json");

					Settings = JsonConvert.DeserializeObject<SettingsObject>(json);
					Settings.Check(w);
				}
			}
			catch { }

			WriteFile();
		}

		private static void WriteFile()
		{
			var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);

			try
			{
				File.WriteAllText("GameOfLife.json", json);
			}
			catch { }
		}
	}

	internal class SettingsObject
	{
		[JsonProperty("FPS", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FPS;
		[JsonProperty("VSync", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool VSync;

		[JsonProperty("FillScreens", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool FillScreens = true;

		[JsonProperty("CellSize", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CellSize;
		/*
		[JsonProperty("CellsX", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CellsX;
		[JsonProperty("CellsY", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CellsY;*/
		[JsonProperty("BlurFrames", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int BlurFrames;

		[JsonProperty("BottomValue", DefaultValueHandling = DefaultValueHandling.Include)]
		public int BottomValue = 20;

		[JsonProperty("Images", DefaultValueHandling = DefaultValueHandling.Include)]
		public string[] Images = { "image.png" };

		public void Check(Window w)
		{
			FPS = Math.Max(1, FPS);//MathHelper.Clamp(FPS, 1, 144);
			//CellsX = MathHelper.Clamp(CellsX, 1, w.Width / 10);
			//CellsY = MathHelper.Clamp(CellsY, 1, w.Height / 10);
			CellSize = Math.Max(1, CellSize);
			BlurFrames = MathHelper.Clamp(BlurFrames, 0, 200);
		}
	}
}
