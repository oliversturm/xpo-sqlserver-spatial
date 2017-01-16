using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
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
            if (value != null)
            {
                Type valueType = value.GetType();
                if (valueType == typeof(SqlGeography) || valueType == typeof(SqlGeometry))
                    return value.ToString();
                //return value;
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
            if (value is string)
                return SqlGeography.Parse((string)value);
            else return value;
        }

        public override object ConvertToStorageType(object value)
        {
            return value == null ? null : ((SqlGeography)value).ToString();
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
            builder.SetSrid(4326);
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
                    Console.WriteLine(polygon.Name);
                    Console.WriteLine(polygon.Polygon);
                }
            }

        }
    }
}
