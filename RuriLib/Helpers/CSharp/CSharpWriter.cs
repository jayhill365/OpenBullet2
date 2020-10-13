﻿using RuriLib.Models.Blocks.Settings;
using RuriLib.Models.Blocks.Settings.Interpolated;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RuriLib.Helpers.CSharp
{
    public class CSharpWriter
    {
        private readonly static CodeGeneratorOptions codeGenOptions = new CodeGeneratorOptions
        {
            BlankLinesBetweenMembers = false
        };

        /// <summary>
        /// Converts a <paramref name="setting"/> to a valid C# formulation.
        /// </summary>
        public static string FromSetting(BlockSetting setting)
        {
            if (setting.InputMode == SettingInputMode.Variable)
                return setting.InputVariableName;

            if (setting.InputMode == SettingInputMode.Interpolated)
            {
                return setting.InterpolatedSetting switch
                {
                    InterpolatedStringSetting x => SerializeInterpString(x.Value),
                    InterpolatedListOfStringsSetting x => SerializeList(x.Value, true),
                    InterpolatedDictionaryOfStringsSetting x => SerializeDictionary(x.Value, true),
                    _ => throw new NotImplementedException()
                };
                
            }

            return setting.FixedSetting switch
            {
                BoolSetting x => ToPrimitive(x.Value),
                ByteArraySetting x => SerializeByteArray(x.Value),
                DictionaryOfStringsSetting x => SerializeDictionary(x.Value),
                FloatSetting x => ToPrimitive(x.Value),
                IntSetting x => ToPrimitive(x.Value),
                ListOfStringsSetting x => SerializeList(x.Value),
                StringSetting x => ToPrimitive(x.Value),
                EnumSetting x => $"{x.EnumType.FullName}.{x.Value}",
                _ => throw new NotImplementedException()
            };
        }

        /// <summary>
        /// Converts a <paramref name="value"/> to a C# primitive.
        /// </summary>
        public static string ToPrimitive(object value)
        {
            using var writer = new StringWriter();
            using var provider = CodeDomProvider.CreateProvider("CSharp");

            provider.GenerateCodeFromExpression(new CodePrimitiveExpression(value), writer, codeGenOptions);
            return writer.ToString();
        }

        /// <summary>
        /// Serializes a literal without splitting it on multiple lines like <see cref="ToPrimitive(object)"/> does..
        /// </summary>
        public static string SerializeString(string value)
            => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        public static string SerializeInterpString(string value)
        {
            StringBuilder sb = new StringBuilder(SerializeString(value))
                .Replace("{", "{{")
                .Replace("}", "}}");

            foreach (Match match in Regex.Matches(value, "<([^>]+)>"))
                sb.Replace(match.Groups[0].Value, '{' + match.Groups[1].Value + '}');

            return '$' + sb.ToString();
        }

        public static string SerializeByteArray(byte[] bytes)
        {
            if (bytes == null)
                return "null";

            using StringWriter writer = new StringWriter();
            writer.Write("new byte[] {");
            writer.Write(string.Join(", ", bytes.Select(b => Convert.ToInt32(b).ToString())));
            writer.Write("}");
            return writer.ToString();
        }

        public static string SerializeList(List<string> list, bool interpolated = false)
        {
            if (list == null)
                return "null";

            using StringWriter writer = new StringWriter();
            writer.Write("new List<string> {");

            var toWrite = list.Select(e => interpolated
                ? SerializeInterpString(e)
                : ToPrimitive(e));

            writer.Write(string.Join(", ", toWrite));
            writer.Write("}");
            return writer.ToString();
        }

        public static string SerializeDictionary(Dictionary<string, string> dict, bool interpolated = false)
        {
            if (dict == null)
                return "null";

            using StringWriter writer = new StringWriter();
            writer.Write("new Dictionary<string, string> {");

            var toWrite = dict.Select(kvp => interpolated
                ? $"{{{SerializeInterpString(kvp.Key)}, {SerializeInterpString(kvp.Value)}}}"
                : $"{{{ToPrimitive(kvp.Key)}, {ToPrimitive(kvp.Value)}}}");

            writer.Write(string.Join(", ", toWrite));
            writer.Write("}");
            return writer.ToString();
        }
    }
}
