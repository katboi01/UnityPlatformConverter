using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommandLine;

namespace UnityPlatformConverter
{
    class Program
    {
        AssetsManager am;

        public class Options
        {
            [Option('d', "directoryMode", Required = false, HelpText = "Set to work on directory instead of file")]
            public bool DirectoryMode { get; set; } = false;

            [Option('s', "silent", Required = false, HelpText = "Set as true to hide console messages")]
            public bool Silent { get; set; } = false;

            [Option('p', "platform", Required = true, HelpText = "platform integer. Common platforms: 5-pc 13-android 20-webgl")]
            public int Platform { get; set; }

            [Option('i', "input", Required = true, HelpText = "input file/directory with extension")]
            public string Input { get; set; }

            [Option('o', "output", Required = true, HelpText = "output file/directory with extension")]
            public string Output { get; set; }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Program p = new Program();

            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       if (o.DirectoryMode)
                       {
                           Program p = new Program();
                           p.ChangeDirectoryVersion(o.Platform, o.Input, o.Output, o.Silent);
                       }
                       else
                       {
                           Program p = new Program();
                           p.ChangeFileVersion(o.Platform, o.Input, o.Output, o.Silent);
                       }
                   });
        }

        private void ChangeFileVersion(int platformId, string input, string output, bool silent)
        {
            am = new AssetsManager();
            am.LoadClassPackage(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), @"classdata.tpk"));

            //Load file
            string selectedFile = input;
            BundleFileInstance bundleInst = null;
            try
            {
                bundleInst = am.LoadBundleFile(selectedFile, false);
                //Decompress the file to memory
                bundleInst.file = DecompressToMemory(bundleInst);
            }
            catch
            {
                if (!silent) Console.WriteLine($"Error: {Path.GetFileName(selectedFile)} is not a valid bundle file");
                return;
            }

            AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bundleInst, 0);
            am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);

            inst.file.Metadata.TargetPlatform = (uint)platformId; //5-pc //13-android //20-webgl

            //commit changes
            byte[] newAssetData;
            using (MemoryStream stream = new MemoryStream())
            {
                using (AssetsFileWriter writer = new AssetsFileWriter(stream))
                {
                    inst.file.Write(writer, 0, new List<AssetsReplacer>() { });
                    newAssetData = stream.ToArray();
                }
            }

            BundleReplacerFromMemory bunRepl = new BundleReplacerFromMemory(inst.name, null, true, newAssetData, -1);

            //write a modified file (temp)
            string tempFile = Path.GetTempFileName();
            using (var stream = File.OpenWrite(tempFile))
            using (var writer = new AssetsFileWriter(stream))
            {
                bundleInst.file.Write(writer, new List<BundleReplacer>() { bunRepl });
            }
            bundleInst.file.Close();

            //load the modified file for compression
            bundleInst = am.LoadBundleFile(tempFile);
            using (var stream = File.OpenWrite(output))
            using (var writer = new AssetsFileWriter(stream))
            {
                bundleInst.file.Pack(bundleInst.file.Reader, writer, AssetBundleCompressionType.LZ4);
            }
            bundleInst.file.Close();

            File.Delete(tempFile);
            if (!silent) Console.WriteLine("complete");
            am.UnloadAll(); //delete this if something breaks
        }

        private void ChangeDirectoryVersion(int platformId, string inputDir, string outputDir, bool silent)
        {
            Directory.CreateDirectory(outputDir);

            am = new AssetsManager();
            am.LoadClassPackage(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), @"classdata.tpk"));
            foreach (var selectedFile in Directory.GetFiles(inputDir))
            {
                if (!silent) Console.WriteLine($"Converting {Path.GetFileName(selectedFile)}");

                //Load file
                BundleFileInstance bundleInst = null;
                try
                {
                    bundleInst = am.LoadBundleFile(selectedFile, false);
                    //Decompress the file to memory
                    bundleInst.file = DecompressToMemory(bundleInst);
                }
                catch
                {
                    if (!silent) Console.WriteLine($"Error: {Path.GetFileName(selectedFile)} is not a valid bundle file");
                    continue;
                }

                AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bundleInst, 0);
                am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);

                inst.file.Metadata.TargetPlatform = (uint)platformId; //5-pc //13-android //20-webgl

                //commit changes
                byte[] newAssetData;
                using (MemoryStream stream = new MemoryStream())
                {
                    using (AssetsFileWriter writer = new AssetsFileWriter(stream))
                    {
                        inst.file.Write(writer, 0, new List<AssetsReplacer>() { });
                        newAssetData = stream.ToArray();
                    }
                }

                BundleReplacerFromMemory bunRepl = new BundleReplacerFromMemory(inst.name, null, true, newAssetData, -1);

                //write a modified file (temp)
                string tempFile = Path.GetTempFileName();
                using (var stream = File.OpenWrite(tempFile))
                using (var writer = new AssetsFileWriter(stream))
                {
                    bundleInst.file.Write(writer, new List<BundleReplacer>() { bunRepl });
                }
                bundleInst.file.Close();

                //load the modified file for compression
                bundleInst = am.LoadBundleFile(tempFile);
                using (var stream = File.OpenWrite(Path.Combine(outputDir, Path.GetFileName(selectedFile))))
                using (var writer = new AssetsFileWriter(stream))
                {
                    bundleInst.file.Pack(bundleInst.file.Reader, writer, AssetBundleCompressionType.LZ4);
                }
                bundleInst.file.Close();

                File.Delete(tempFile);
                am.UnloadAll(); //delete this if something breaks
            }
            if (!silent) Console.WriteLine("complete");
        }

        public static AssetBundleFile DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream));

            bundle.Reader.Close();
            return newBundle;
        }
    }
}
