using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageSheet
{
    class JsonConfig
    {
        public string outputFile { get; set; } = "out.png";
        public double? resize { get; set; }
        public List<List<string>> filesRow { get; set; }
    }

    class Program
    {
        static List<string> supportedFormats = new List<string> { ".png", ".jpg", ".jpeg" };

        static void Main(string[] args)
        {
            var workingDirectory = Environment.CurrentDirectory;
            if (args.Length > 0)
                workingDirectory = Path.Combine(workingDirectory, args[0]);
            Console.WriteLine("Running on " + workingDirectory);

            JsonConfig config;
            if (File.Exists(Path.Combine(workingDirectory, "sheet.json")))
                config = JsonSerializer.Deserialize<JsonConfig>(File.ReadAllText(Path.Combine(workingDirectory, "sheet.json")));
            else
                config = new JsonConfig { filesRow = new List<List<string>> { supportedFormats.Select(row => "*" + row).ToList() } };

            var files = Directory.GetFiles(workingDirectory)
                .Where(row => supportedFormats.Any(r2 => row.ToLower().EndsWith(r2)) && Path.GetFileName(row).ToLower() != config.outputFile.ToLower())
                .OrderBy(row => row)
                .ToList();
            var row = 0;

            //var destBitmap=new Bitmap()

            var height = 0;
            var width = 0;

            Dictionary<string, Image> images = new Dictionary<string, Image>();

            foreach (var ruleRow in config.filesRow) // Will handle the rows
            {
                Console.WriteLine("Row " + row);

                var rowFiles = ruleRow.SelectMany(row => MatchRule(row, files)).ToList();
                // Load all files
                rowFiles.ForEach(row =>
                {
                    Console.WriteLine(row);
                    images.Add(row, Bitmap.FromFile(row));
                });
                height += rowFiles.Max(row => images[row].Height);
                width = Math.Max(width, rowFiles.Sum(row => images[row].Height));
                row++;
            }

            try
            {
                using (var destBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(destBitmap))
                    {
                        var y = 0;
                        foreach (var ruleRow in config.filesRow) // Will handle the rows
                        {
                            var rowFiles = ruleRow.SelectMany(row => MatchRule(row, files)).ToList();
                            var x = 0;
                            foreach (var img in rowFiles.Select(row => images[row]))
                            {
                                g.DrawImage(img, x, y, img.Width, img.Height);
                                x += img.Width;
                            }
                            y += rowFiles.Select(row => images[row]).Max(row => row.Height);
                        }
                    }

                    if (config.resize.HasValue && config.resize.Value != 1.0)
                    {
                        var w = (int)(width * config.resize.Value);
                        var h = (int)(height * config.resize.Value);
                        using (var resized = new Bitmap(w,h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            using(var g=Graphics.FromImage(resized))
                                g.DrawImage(destBitmap, 0, 0, w, h);
                            resized.Save(Path.Combine(workingDirectory, config.outputFile));
                        }
                    }
                    else
                        destBitmap.Save(Path.Combine(workingDirectory, config.outputFile));
                }
            }
            finally
            {
                foreach (var img in images.Values)
                    img.Dispose();
            }
        }

        private static List<string> MatchRule(string rule, List<string> completeFileNames)
        {
            var regs = new Regex("^" + rule.Replace(".", "\\.").Replace("*", ".*") + "$", RegexOptions.IgnoreCase);
            return completeFileNames.Where(row => regs.IsMatch(Path.GetFileName(row))).ToList();
        }
    }
}
