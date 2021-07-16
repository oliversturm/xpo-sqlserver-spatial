using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace XPOSqlServerSpatialNTS
{

    public class GISProvider : MSSqlConnectionProvider
    {
        public GISProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
          : base(connection, autoCreateOption)
        {
        }

        protected override void GetValues(IDataReader reader, Type[] fieldTypes, object[] values)
        {
            // A direct read of the geometry field with the GetValue method will produce Microsoft.SqlServer.Types exception.
            // So it's necessary to determine that field is geometry and read it as bytes.
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.GetDataTypeName(i).EndsWith("sys.geometry") ||
                    reader.IsDBNull(i))
                {
                    values[i] = reader.GetValue(i);
                }
                else
                {
                    // Cast to SqlDataReader to read the bytes  
                    // values[i] = (reader as SqlDataReader).GetSqlBytes(i).Value;

                    // Or use memory stream to read the field
                    values[i] = GetBytes(reader, i);
                }
            }
        }

        private static byte[] GetBytes(IDataReader reader, int i)
        {
            using MemoryStream memoryStream = new();
            byte[] buffer = new byte[8192];
            long offset = 0;
            long bytesRead = 0;
            do
            {
                bytesRead = reader.GetBytes(i, offset, buffer, 0, buffer.Length);
                memoryStream.Write(buffer, 0, (int)bytesRead);
                offset += bytesRead;
            } while (bytesRead >= buffer.Length);
            return memoryStream.ToArray();
        }
    }

    public class PolygonData : XPObject
    {
        public PolygonData(Session session)
          : base(session) { }

        private string name;
        public string Name
        {
            get { return name; }
            set { SetPropertyValue(nameof(Name), ref name, value); }
        }

        Geometry fGeom;
        [ValueConverter(typeof(GeometryConverter))]
        [DbType("geometry")]
        public Geometry Geom
        {
            get { return fGeom; }
            set { SetPropertyValue(nameof(Geom), ref fGeom, value); }
        }
    }

    public class GeometryConverter : ValueConverter
    {
        public override object ConvertFromStorageType(object value)
        {
            if (value != null)
            {
                var geometryReader = new SqlServerBytesReader { IsGeography = false, RepairRings = true };
                return geometryReader.Read((byte[])value);
            }
            else
                return null;
        }

        public override object ConvertToStorageType(object value)
        {
            if (value == null) return null;

            if (value is Geometry geometry)
            {
                return new SqlServerBytesWriter().Write(geometry);
            }
            else return value;
        }

        public override Type StorageType
        {
            get { return typeof(byte[]); }
        }
    }

    class Program
    {
        private static Polygon CreatePolygon()
        {
            Polygon polygon = new(
                new LinearRing(
                    new Coordinate[] {
                    new Coordinate(734352.631, 6909426.235),
                    new Coordinate(542625.097, 5882056.051),
                    new Coordinate(1678520.300, 5730121.024),
                    new Coordinate(1888335.337, 6663436.191),
                    new Coordinate(1468705.262, 6970923.746),
                    new Coordinate(734352.631, 6909426.235)
            }));
            polygon.SRID = 3857;

            return polygon;
        }

        static void Main(string[] args)
        {
            XpoDefault.DataLayer =
              new SimpleDataLayer(new GISProvider(
                new SqlConnection("data source=localhost;integrated security=SSPI;initial catalog=XPOSql2008Spatial"),
                AutoCreateOption.DatabaseAndSchema));

            using (UnitOfWork uow = new())
            {
                uow.ClearDatabase();
                uow.UpdateSchema(typeof(PolygonData));
            }

            using (UnitOfWork uow = new())
            {
                new PolygonData(uow)
                {
                    Name = "Test 1",
                    Geom = CreatePolygon()
                }.Save();
                uow.CommitChanges();
            }

            using (UnitOfWork uow = new())
            {
                var polygons = new XPCollection<PolygonData>();
                foreach (var polygon in polygons)
                {
                    Console.WriteLine("Name: " + polygon.Name);
                    Console.WriteLine("SRID: " + polygon.Geom.SRID);
                    Console.WriteLine("Polygon: " + polygon.Geom.ToString());
                }
            }

        }
    }
}
