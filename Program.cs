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
        string LoadDirectoryPath, SaveDirectoryPath;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Program p = new Program();
            p.ChangeVersion();
        }

        private void ChangeVersion()
        {
            LoadDirectoryPath = Path.Combine(Environment.CurrentDirectory, @"Input\");
            SaveDirectoryPath = Path.Combine(Environment.CurrentDirectory, @"Output\");
            Directory.CreateDirectory(LoadDirectoryPath);
            Directory.CreateDirectory(SaveDirectoryPath);

            am = new AssetsManager();
            am.LoadClassPackage(Path.Combine(Environment.CurrentDirectory, @"classdata.tpk"));

            int fileCount = Directory.GetFiles(LoadDirectoryPath).Length;
            //For each file in Data/Scenarios/ (translated files), look for the original file and...
            foreach (string file in Directory.GetFiles(LoadDirectoryPath))
            {
                Console.WriteLine(file);
                Console.WriteLine(fileCount-- + " files left");
                //Load file
                string selectedFile = file; //Path.Combine(SaveDirectoryPath, Path.GetFileNameWithoutExtension(file));
                BundleFileInstance bundleInst = am.LoadBundleFile(selectedFile, false);

                //Decompress the file to memory
                bundleInst.file = DecompressToMemory(bundleInst);

                AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bundleInst, 0);
                am.LoadClassDatabaseFromPackage(inst.file.typeTree.unityVersion);

                inst.file.typeTree.version = 20; //5-pc //6-android? //20-webgl

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
                using (var stream = File.OpenWrite(selectedFile + "_temp"))
                using (var writer = new AssetsFileWriter(stream))
                {
                    bundleInst.file.Write(writer, new List<BundleReplacer>() { bunRepl });
                }
                bundleInst.file.Close();

                //load the modified file for compression
                bundleInst = am.LoadBundleFile(selectedFile + "_temp");
                using (var stream = File.OpenWrite(Path.Combine(SaveDirectoryPath, Path.GetFileName(selectedFile))))
                using (var writer = new AssetsFileWriter(stream))
                {
                    bundleInst.file.Pack(bundleInst.file.reader, writer, AssetBundleCompressionType.LZ4);
                }
                bundleInst.file.Close();

                File.Delete(selectedFile + "_temp");
                am.UnloadAll(); //delete this if something breaks

                Console.WriteLine("Work finished, press enter...");
                Console.ReadLine();
            }
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
