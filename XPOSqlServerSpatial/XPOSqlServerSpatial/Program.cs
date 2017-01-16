using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XPOSqlServerSpatial
{

    public class GISProvider : MSSqlConnectionProvider
    {
        public GISProvider(IDbConnection connection, AutoCreateOption autoCreateOption)
          : base(connection, autoCreateOption)
        {
        }

        protected override object ReformatReadValue(object value, ReformatReadValueArgs args)
        {
            // This implementation deactivates the default behavior of the base 
            // class logic, because the conversion step is not necessary for the types
            // SqlGeography and SqlGeometry, and because the attempt at conversion
            // results in exceptions since there is no automatic conversion mechanism.
            if (value != null)
            {
                Type valueType = value.GetType();
                if (valueType == typeof(SqlGeography) || valueType == typeof(SqlGeometry))
                    return value;
            }
            return base.ReformatReadValue(value, args);
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
            set { SetPropertyValue("Name", ref name, value); }
        }

        SqlGeography polygon;
        [ValueConverter(typeof(GeographyConverter))]
        [DbType("geography")]
        public SqlGeography Polygon
        {
            get { return polygon; }
            set { SetPropertyValue("Polygon", ref polygon, value); }
        }
    }

    public class GeographyConverter : ValueConverter
    {
        public override object ConvertFromStorageType(object value)
        {
            // We're ignoring the request to convert here, knowing that the loaded
            // object is already the correct type because SqlClient returns it 
            // that way.
            return value;
        }

        public override object ConvertToStorageType(object value)
        {
            if (value == null) return null;

            // this mechanism persists the srid to SQL Server - 
            // better than using WKT because it doesn't contain srid at all
            var sqlg = ((SqlGeography)value);
            return sqlg.Serialize();
        }

        public override Type StorageType
        {
            get { return typeof(string); }
        }
    }

    class Program
    {

        private static SqlGeography CreatePolygon()
        {
            SqlGeographyBuilder builder = new SqlGeographyBuilder();
            // Explicitly using a different SRID here to check serialization
            //            builder.SetSrid(4326);
            builder.SetSrid(4322);
            builder.BeginGeography(OpenGisGeographyType.Polygon);
            builder.BeginFigure(55.36728, -2.74941);
            builder.AddLine(55.40002, -2.68289);
            builder.AddLine(55.39908, -2.74913);
            builder.AddLine(55.36728, -2.74941);
            builder.EndFigure();
            builder.EndGeography();
            return builder.ConstructedGeography;
        }

        static void Main(string[] args)
        {

            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            XpoDefault.DataLayer =
              new SimpleDataLayer(new GISProvider(
                // need Type System Version here because SqlClient will otherwise blindly try to load 
                // version 10.0.0.0 of Microsoft.SqlServer.Types regardless of project references
                  new SqlConnection("data source=.\\SQLEXPRESS;integrated security=SSPI;Type System Version=SQL Server 2012;initial catalog=XPOSql2008Spatial"),
                //    new SqlConnection("data source=.\\SQLEXPRESS;integrated security=SSPI;initial catalog=XPOSql2008Spatial"),
                AutoCreateOption.DatabaseAndSchema));

            using (UnitOfWork uow = new UnitOfWork())
            {
                uow.ClearDatabase();
                uow.UpdateSchema(typeof(PolygonData));
            }

            using (UnitOfWork uow = new UnitOfWork())
            {
                new PolygonData(uow)
                {
                    Name = "Test 1",
                    Polygon = CreatePolygon()
                }.Save();
                uow.CommitChanges();
            }

            using (UnitOfWork uow = new UnitOfWork())
            {
                var polygons = new XPCollection<PolygonData>();
                foreach (var polygon in polygons)
                {
                    Console.WriteLine("Name: " + polygon.Name);
                    Console.WriteLine("SRID: " + polygon.Polygon.STSrid);
                    Console.WriteLine("Polygon: " + polygon.Polygon);
                }
            }

        }
    }
}
