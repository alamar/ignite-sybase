// ReSharper disable All
using System.Text;
using Apache.Ignite.Core.Binary;
using Apache.Ignite.Core.Cache.Configuration;

namespace Apache.Ignite.Sybase.Ingest.Cache
{
    public class MRetRethidsubcategoryderived : IBinarizable, ICanReadFromRecordBuffer
    {
        [QuerySqlField(Name = "rethidsubcategoryderived")] public long Rethidsubcategoryderived { get; set; }
        [QuerySqlField(Name = "description")] public string Description { get; set; }
        [QuerySqlField(Name = "displayorder")] public long Displayorder { get; set; }
        [QuerySqlField(Name = "name")] public string Name { get; set; }
        [QuerySqlField(Name = "shortname")] public string Shortname { get; set; }
        [QuerySqlField(Name = "startrange")] public double Startrange { get; set; }
        [QuerySqlField(Name = "endrange")] public double Endrange { get; set; }
        [QuerySqlField(Name = "retsubcategoryderived")] public long Retsubcategoryderived { get; set; }
        [QuerySqlField(Name = "retcategoryderived")] public long Retcategoryderived { get; set; }
        [QuerySqlField(Name = "retcategorygroupderived")] public long Retcategorygroupderived { get; set; }
        [QuerySqlField(Name = "retsupercategoryderived")] public long Retsupercategoryderived { get; set; }
        [QuerySqlField(Name = "altbusiness")] public long Altbusiness { get; set; }

        public void WriteBinary(IBinaryWriter writer)
        {
            writer.WriteLong("rethidsubcategoryderived", Rethidsubcategoryderived);
            writer.WriteString("description", Description);
            writer.WriteLong("displayorder", Displayorder);
            writer.WriteString("name", Name);
            writer.WriteString("shortname", Shortname);
            writer.WriteDouble("startrange", Startrange);
            writer.WriteDouble("endrange", Endrange);
            writer.WriteLong("retsubcategoryderived", Retsubcategoryderived);
            writer.WriteLong("retcategoryderived", Retcategoryderived);
            writer.WriteLong("retcategorygroupderived", Retcategorygroupderived);
            writer.WriteLong("retsupercategoryderived", Retsupercategoryderived);
            writer.WriteLong("altbusiness", Altbusiness);
        }

        public void ReadBinary(IBinaryReader reader)
        {
            Rethidsubcategoryderived = reader.ReadLong("rethidsubcategoryderived");
            Description = reader.ReadString("description");
            Displayorder = reader.ReadLong("displayorder");
            Name = reader.ReadString("name");
            Shortname = reader.ReadString("shortname");
            Startrange = reader.ReadDouble("startrange");
            Endrange = reader.ReadDouble("endrange");
            Retsubcategoryderived = reader.ReadLong("retsubcategoryderived");
            Retcategoryderived = reader.ReadLong("retcategoryderived");
            Retcategorygroupderived = reader.ReadLong("retcategorygroupderived");
            Retsupercategoryderived = reader.ReadLong("retsupercategoryderived");
            Altbusiness = reader.ReadLong("altbusiness");
        }

        public unsafe void ReadFromRecordBuffer(byte[] buffer)
        {
            fixed (byte* p = &buffer[0])
            {
                Rethidsubcategoryderived = *(long*) (p + 0);
                Description = Encoding.ASCII.GetString(buffer, 8, 256).TrimEnd();
                Displayorder = *(long*) (p + 264);
                Name = Encoding.ASCII.GetString(buffer, 272, 128).TrimEnd();
                Shortname = Encoding.ASCII.GetString(buffer, 400, 128).TrimEnd();
                Startrange = *(double*) (p + 528);
                Endrange = *(double*) (p + 536);
                Retsubcategoryderived = *(long*) (p + 544);
                Retcategoryderived = *(long*) (p + 552);
                Retcategorygroupderived = *(long*) (p + 560);
                Retsupercategoryderived = *(long*) (p + 568);
                Altbusiness = *(long*) (p + 576);
            }
        }
    }
}
