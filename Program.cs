using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityPlatformConverter
{
    class Program
    {
        AssetsManager am;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (args.Length != 3)
            {
                Console.WriteLine("UnityPlatformConverter by Katboi01");
                Console.WriteLine("Not enough parameters. Expected input: path_to_.exe targetPlatformID inputFilePath outputFilePath");
                Console.WriteLine("Common platforms: 5-pc 13-android 20-webgl");
                Console.Read();
            }
            else
            {
                Program p = new Program();
                p.ChangeVersion(args[0], args[1], args[2]);
            }
        }

        private void ChangeVersion(string platformId, string input, string output)
        {
            am = new AssetsManager();
            am.LoadClassPackage(Path.Combine(Environment.CurrentDirectory, @"classdata.tpk"));

            //Load file
            string selectedFile = input;
            BundleFileInstance bundleInst = am.LoadBundleFile(selectedFile, false);

            //Decompress the file to memory
            bundleInst.file = DecompressToMemory(bundleInst);

            AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bundleInst, 0);
            am.LoadClassDatabaseFromPackage(inst.file.typeTree.unityVersion);

            inst.file.typeTree.version = (uint)int.Parse(platformId); //5-pc //13-android //20-webgl

            //commit changes
            byte[] newAssetData;
            using (MemoryStream stream = new MemoryStream())
            {
                using (AssetsFileWriter writer = new AssetsFileWriter(stream))
                {
                    inst.file.Write(writer, 0, new List<AssetsReplacer>() { }, 0);
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
                bundleInst.file.Pack(bundleInst.file.reader, writer, AssetBundleCompressionType.LZ4);
            }
            bundleInst.file.Close();

            File.Delete(tempFile);
            Console.WriteLine("complete");
            am.UnloadAll(); //delete this if something breaks
        }

        public static AssetBundleFile DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            return newBundle;
        }
    }
}
