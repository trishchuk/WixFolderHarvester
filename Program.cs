﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace WixXmlGenerator
{
    internal static class DictionaryExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            if (dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }
    }

    class Program
    {
        private const string DirectoryPrefix = "dir";
        private const string FilePrefix = "fil";
        private const string ComponentPrefix = "cmp";

        private const int HashLength = 16;

        public static void Main(string[] args)
        {
            try
            {
                var arguments = new Dictionary<string, string>();

                foreach (var arg in args)
                {
                    var argKeyValue = arg.Split('=');
                    arguments[argKeyValue[0].ToLowerInvariant()] = argKeyValue[1].Replace("\"", "").Trim();
                }

                var rootFolderPath = arguments["-directory"];
                var referencePath = $"$({arguments.GetOrDefault("-preprocessorvariable", "var.HarvestPath")})";
                var componentGroupName = arguments.GetOrDefault("-componentgroup", "HeatGenerated");
                var rootFolderRefId = arguments.GetOrDefault("-directoryrefid", "INSTALLFOLDER");
                var outputFilePath = arguments["-output"];
                var excludeFileExtensions = new HashSet<string>(
                    arguments
                        .GetOrDefault("-excludeextensions", "")
                        .Split(new[]  { ';' }, StringSplitOptions.RemoveEmptyEntries));

                var builder = new StringBuilder();

                builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                builder.AppendLine("<Wix xmlns=\"http://schemas.microsoft.com/wix/2006/wi\">");

                var components = ProcessFilesFragment(rootFolderPath, referencePath, rootFolderRefId, excludeFileExtensions, builder);

                ProcessComponentsFragment(componentGroupName, components, builder);

                builder.AppendLine("</Wix>");

                var xml = builder.ToString();

                SaveFormattedXmlToFile(xml, outputFilePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occured. " + e.Message);
                Console.WriteLine(e);
            }
        }

        private static List<string> ProcessFilesFragment(string rootFolderPath, string referencePath, string rootFolderRefId, HashSet<string> excludeExtensions, StringBuilder builder)
        {
            builder.AppendLine("<Fragment>");

            var components = new List<string>();

            ProcessRootFolder(rootFolderPath, referencePath, rootFolderRefId, excludeExtensions, components, builder);

            builder.AppendLine("</Fragment>");
            return components;
        }

        private static void ProcessComponentsFragment(string componentGroupName, List<string> components, StringBuilder builder)
        {
            builder.AppendLine("<Fragment>");

            builder.AppendLine($"<ComponentGroup Id=\"{componentGroupName}\">");

            foreach (var component in components)
            {
                builder.AppendLine($"<ComponentRef Id=\"{component}\" />");
            }

            builder.AppendLine("</ComponentGroup>");
            builder.AppendLine("</Fragment>");
        }

        private static void ProcessRootFolder(string rootFolderPath, string referencePath, string rootFolderRefId, HashSet<string> excludeExtensions, List<string> components, StringBuilder stringBuilder)
        {
            var folderName = Path.GetFileName(rootFolderPath);
            var folderId = GenerateIdentifier(DirectoryPrefix, rootFolderPath);

            stringBuilder.AppendLine($"<DirectoryRef Id=\"{rootFolderRefId}\">");

            ProcessFiles(rootFolderPath, referencePath, "", folderId, excludeExtensions, components, stringBuilder);

            ProcessChildFolders(rootFolderPath, referencePath, excludeExtensions, components, stringBuilder);

            stringBuilder.AppendLine($"</DirectoryRef>");
        }

        private static void ProcessChildFolders(string folderPath, string referencePath, HashSet<string> excludeExtensions, List<string> components, StringBuilder stringBuilder)
        {
            var folders = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var childFolder in folders)
            {
                var childFolderName = Path.GetFileName(childFolder);
                var childReferencePath = Path.Combine(referencePath, childFolderName);
                ProcessFolder(childFolder, childReferencePath, excludeExtensions, components, stringBuilder);
            }
        }

        private static void ProcessFolder(string folderPath, string referencePath, HashSet<string> excludeExtensions, List<string> components, StringBuilder stringBuilder)
        {
            var folderName = Path.GetFileName(folderPath);
            var folderId = GenerateIdentifier(DirectoryPrefix, folderPath);

            stringBuilder.AppendLine($"<Directory Id=\"{folderId}\" Name=\"{folderName}\">");

            ProcessFiles(folderPath, referencePath, folderName, folderId, excludeExtensions, components, stringBuilder);

            ProcessChildFolders(folderPath, referencePath, excludeExtensions, components, stringBuilder);

            stringBuilder.AppendLine($"</Directory>");
        }

        private static void ProcessFiles(string folderPath, string referencePath, string folderName, string folderId, HashSet<string> excludeExtensions, List<string> components, StringBuilder stringBuilder)
        {
            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file);
                if (excludeExtensions.Contains(extension))
                {
                    continue;
                }
                var fileId = GenerateIdentifier(FilePrefix, folderId, fileName);
                var componentId = GenerateIdentifier(ComponentPrefix, folderId, fileId);
                var filePath = Path.Combine(referencePath, fileName);

                components.Add(componentId);

                stringBuilder.AppendLine($"<Component Id=\"{componentId}\" Guid=\"*\">");
                stringBuilder.AppendLine($"<File Id=\"{fileId}\" KeyPath=\"yes\" Source=\"{filePath}\" />");
                stringBuilder.AppendLine($"</Component>");
            }
        }

        private static string GenerateIdentifier(string prefix, params string[] args)
        {
            var stringData = String.Join("|", args);
            var data = Encoding.Unicode.GetBytes(stringData);

            byte[] hash;
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                hash = md5.ComputeHash(data);
            }

            var identifier = new StringBuilder(prefix.Length + 2 * HashLength, prefix.Length + 2 * HashLength);
            identifier.Append(prefix);

            for (int i = 0; i < HashLength; i++)
            {
                identifier.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture.NumberFormat));
            }

            return identifier.ToString();
        }

        private static void SaveFormattedXmlToFile(string xmlString, string targetFilePath)
        {
            try
            {
                string formattedXml;

                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xmlString);

                using (var ms = new MemoryStream())
                {
                    using (var xmlWriter = new XmlTextWriter(ms, Encoding.Unicode))
                    {
                        xmlWriter.Formatting = Formatting.Indented;
                        xmlDocument.WriteContentTo(xmlWriter);

                        xmlWriter.Flush();
                        ms.Flush();

                        ms.Position = 0;
                        using (var sr = new StreamReader(ms))
                        {
                            formattedXml = sr.ReadToEnd();
                        }
                    }
                }

                using (var sw = new StreamWriter(targetFilePath))
                {
                    sw.Write(formattedXml);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
