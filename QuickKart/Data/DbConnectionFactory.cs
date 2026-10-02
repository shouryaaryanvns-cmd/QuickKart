using System.Configuration;
using System.Data.SqlClient;

namespace QuickKart.Data
{
    public static class DbConnectionFactory
    {
        public static SqlConnection Create()
        {
            string connectionString =
                ConfigurationManager.ConnectionStrings["QuickKartConnection"].ConnectionString;

            return new SqlConnection(connectionString);
        }
    }
}
