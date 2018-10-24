using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Apache.Ignite.Sybase.Ingest.Common;
using Apache.Ignite.Sybase.Ingest.Parsers;

namespace Apache.Ignite.Sybase.Ingest.Cache
{
    public static class ModelClassGenerator
    {
        private static readonly Lazy<string[]> Template = new Lazy<string[]>(LoadTemplate);

        private static string[] LoadTemplate()
        {
            var asmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var codePath = Path.Combine(asmPath, "..", "..", "..", "Cache", nameof(ModelClassTemplate) + ".cs");

            return File.ReadAllLines(codePath);
        }

        public static string GenerateClass(RecordDescriptor desc)
        {
            Arg.NotNull(desc, nameof(desc));

            var template = Template.Value;
            var lines = GetLines(template, desc);

            return string.Join(Environment.NewLine, lines);
        }

        private static IEnumerable<string> GetLines(IEnumerable<string> template, RecordDescriptor desc)
        {
            foreach (var line in template)
            {
                var l = line.Trim();

                if (l.StartsWith("public class"))
                {
                    yield return line
                        .Replace(nameof(ModelClassTemplate), desc.TableName)
                        .Replace(".", "__");
                }
                else if (l.StartsWith("[QuerySqlField]"))
                {
                    foreach (var field in desc.Fields)
                    {
                        yield return line
                            .Replace(nameof(ModelClassTemplate.FieldTemplate), field.Name)
                            .Replace("string", field.Type.GetShortTypeName());
                    }
                }
                else
                {
                    yield return line;
                }
            }
        }
    }
}
