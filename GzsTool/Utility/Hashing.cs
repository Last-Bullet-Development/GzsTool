﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GzsTool.PathId;

namespace GzsTool.Utility
{
    internal static class Hashing
    {
        private static readonly MD5 Md5 = MD5.Create();
        private static readonly PathIdFile PathIdFile = new PathIdFile();
        private static readonly Dictionary<ulong, string> HashNameDictionary = new Dictionary<ulong, string>();

        private static readonly Dictionary<byte[], string> Md5HashNameDictionary =
            new Dictionary<byte[], string>(new StructuralEqualityComparer<byte[]>());

        private static readonly List<string> FileExtensions = new List<string>
        {
            "1.ftexs",
            "1.nav2",
            "2.ftexs",
            "3.ftexs",
            "4.ftexs",
            "5.ftexs",
            "6.ftexs",
            "ag.evf",
            "aia",
            "aib",
            "aibc",
            "aig",
            "aigc",
            "aim",
            "aip",
            "ait",
            "atsh",
            "bnd",
            "bnk",
            "cc.evf",
            "clo",
            "csnav",
            "dat",
            "des",
            "dnav",
            "dnav2",
            "eng.lng",
            "ese",
            "evb",
            "evf",
            "fag",
            "fage",
            "fago",
            "fagp",
            "fagx",
            "fclo",
            "fcnp",
            "fcnpx",
            "fdes",
            "fdmg",
            "ffnt",
            "fmdl",
            "fmdlb",
            "fmtt",
            "fnt",
            "fova",
            "fox",
            "fox2",
            "fpk",
            "fpkd",
            "fpkl",
            "frdv",
            "fre.lng",
            "frig",
            "frt",
            "fsd",
            "fsm",
            "fsml",
            "fsop",
            "fstb",
            "ftex",
            "fv2",
            "fx.evf",
            "fxp",
            "gani",
            "geom",
            "ger.lng",
            "gpfp",
            "grxla",
            "grxoc",
            "gskl",
            "htre",
            "info",
            "ita.lng",
            "jpn.lng",
            "json",
            "lad",
            "ladb",
            "lani",
            "las",
            "lba",
            "lng",
            "lpsh",
            "lua",
            "mas",
            "mbl",
            "mog",
            "mtar",
            "mtl",
            "nav2",
            "nta",
            "obr",
            "obrb",
            "parts",
            "path",
            "pftxs",
            "ph",
            "phep",
            "phsd",
            "por.lng",
            "qar",
            "rbs",
            "rdb",
            "rdf",
            "rnav",
            "rus.lng",
            "sad",
            "sand",
            "sani",
            "sbp",
            "sd.evf",
            "sdf",
            "sim",
            "simep",
            "snav",
            "spa.lng",
            "spch",
            "sub",
            "subp",
            "tgt",
            "tre2",
            "txt",
            "uia",
            "uif",
            "uig",
            "uigb",
            "uil",
            "uilb",
            "utxl",
            "veh",
            "vfx",
            "vfxbin",
            "vfxdb",
            "vnav",
            "vo.evf",
            "vpc",
            "wem",
            "xml"
        };

        private static readonly Dictionary<ulong, string> ExtensionsMap = FileExtensions.ToDictionary(HashFileExtension);

        private static ulong HashFileExtension(string fileExtension)
        {
            return HashFileName(fileExtension, false) & 0x1FFF;
        }

        private static ulong HashFileName(string text, bool removeExtension = true)
        {
            if (removeExtension)
            {
                int index = text.IndexOf('.');
                text = index == -1 ? text : text.Substring(0, index);
            }
            bool setFlag = false;
            text = text.TrimStart('/');
            if (text.StartsWith("Assets/"))
            {
                text = text.Substring("Assets/".Length);
                setFlag = true;
            }

            const ulong seed0 = 0x9ae16a3b2f90404f;
            byte[] seed1Bytes = new byte[sizeof(ulong)];
            for (int i = text.Length - 1, j = 0; i >= 0 && j < sizeof(ulong); i--, j++)
            {
                seed1Bytes[j] = Convert.ToByte(text[i]);
            }
            ulong seed1 = BitConverter.ToUInt64(seed1Bytes, 0);
            ulong maskedHash = CityHash.CityHash.CityHash64WithSeeds(text, seed0, seed1) & 0x3FFFFFFFFFFFF;
            return setFlag ? maskedHash | 0x4000000000000 : maskedHash;
        }
        
        public static ulong HashFileNameLegacy(string text, bool removeExtension = true)
        {
            if (removeExtension)
            {
                int index = text.IndexOf('.');
                text = index == -1 ? text : text.Substring(0, index);
            }

            const ulong seed0 = 0x9ae16a3b2f90404f;
            ulong seed1 = text.Length > 0 ? (uint)((text[0]) << 16) + (uint)text.Length : 0;
            return CityHash.CityHash.CityHash64WithSeeds(text + "\0", seed0, seed1) & 0xFFFFFFFFFFFF;
        }

        public static ulong HashFileNameWithExtension(string filePath)
        {
            filePath = DenormalizeFilePath(filePath);
            var lookupableExtensions = ExtensionsMap
                .Where(e => e.Value != "" && filePath.EndsWith(e.Value, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            string hashablePart = filePath;
            ulong typeId = 0;
            if (lookupableExtensions.Count() == 1)
            {
                var lookupableExtension = lookupableExtensions.Single();
                typeId = lookupableExtension.Key;

                int extensionIndex = hashablePart.LastIndexOf(
                    "." + lookupableExtension.Value,
                    StringComparison.InvariantCultureIgnoreCase);
                hashablePart = hashablePart.Substring(0, extensionIndex);
            }
            ulong hash = HashFileName(hashablePart);
            hash = (typeId << 51) | hash;
            return hash;
        }

        internal static string NormalizeFilePath(string filePath)
        {
            return filePath.Replace("/", "\\").TrimStart('\\');
        }

        private static string DenormalizeFilePath(string filePath)
        {
            return filePath.Replace("\\", "/");
        }

        internal static bool TryGetFileNameFromHash(ulong hash, out string fileName)
        {
            bool foundFileName = true;
            string filePath;
            string fileExtension;

            ulong extensionHash = hash >> 51;
            ulong pathHash = hash & 0x3FFFFFFFFFFFF;

            fileName = "";
            if (!HashNameDictionary.TryGetValue(pathHash, out filePath))
            {
                filePath = pathHash.ToString("x");
                foundFileName = false; 
            }

            fileName += filePath;

            if (!ExtensionsMap.TryGetValue(extensionHash, out fileExtension))
            {
                fileExtension = "_unknown";
                foundFileName = false;
            }
            else
            {
                fileName += ".";
            }
            fileName += fileExtension;
            
            DebugCompareHash(foundFileName, hash, fileName);

            return foundFileName;
        }

        // TODO: Remove after testing that the hashing works correctly
        [Conditional("DEBUG")]
        private static void DebugCompareHash(bool foundFileName, ulong hash, string fileName)
        {
            if (foundFileName)
            {
                ulong hashTest = Hashing.HashFileNameWithExtension(fileName);
                if (hash != hashTest)
                {
                    Debug.WriteLine("{0};{1:x};{2:x};{3:x}", fileName, hash, hashTest, (hashTest - hash));
                }
            }
        }

        public static void ReadDictionary(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                ulong hash = HashFileName(line) & 0x3FFFFFFFFFFFF;
                if (HashNameDictionary.ContainsKey(hash) == false)
                {
                    HashNameDictionary.Add(hash, line);
                }
            }
        }

        internal static byte[] Md5Hash(byte[] buffer)
        {
            return Md5.ComputeHash(buffer);
        }

        internal static byte[] Md5HashText(string text)
        {
            return Md5.ComputeHash(Encoding.Default.GetBytes(text));
        }

        public static void ReadMd5Dictionary(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                byte[] md5Hash = Md5HashText(line);
                if (Md5HashNameDictionary.ContainsKey(md5Hash) == false)
                {
                    Md5HashNameDictionary.Add(md5Hash, line);
                }
            }
        }

        internal static bool TryGetFileNameFromMd5Hash(byte[] md5Hash, string entryName, out string fileName)
        {
            if (Md5HashNameDictionary.TryGetValue(md5Hash, out fileName) == false)
            {
                fileName = string.Format("{0}{1}", BitConverter.ToString(md5Hash).Replace("-", ""),
                    GetFileExtension(entryName));
                return false;
            }
            return true;
        }

        private static string GetFileExtension(string entryName)
        {
            string extension = "";
            int index = entryName.LastIndexOf(".", StringComparison.Ordinal);
            if (index != -1)
            {
                extension = entryName.Substring(index, entryName.Length - index);
            }

            return extension;
        }

        public static void ReadPs3PathIdFile(string path)
        {
            using (FileStream input = new FileStream(path, FileMode.Open))
            {
                PathIdFile.Read(input);
            }
        }
    }
}
