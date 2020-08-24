using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TracingDemo.GrpcService.Providers
{
    public interface IGreetingRepository : IDisposable
    {
        public Task<int> InsertGreeting(GreetingRecord record, CancellationToken cancellationToken);
    }

    public class GreetingRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Utc { get; set; }
    }

    sealed class SqlGreetingRepository : IGreetingRepository
    {
        private readonly SqlConnection _connection;

        public SqlGreetingRepository(IConfiguration config)
        {
            // construct connection string, allowing password to be stored in separate secret
            string connString = config.GetConnectionString("Sql") ?? throw new ArgumentNullException();
            string sqlPassword = config["Passwords:Sql"];

            _connection = new SqlConnection(string.Format(connString, sqlPassword));
        }

        public async Task<int> InsertGreeting(GreetingRecord record, CancellationToken cancellationToken)
        {
            try
            {
                if (_connection.State == ConnectionState.Closed || _connection.State == ConnectionState.Broken)
                    await _connection.OpenAsync(cancellationToken);

                const string sqlText = "INSERT INTO dbo.Greetings (Name,Utc) VALUES (@name,@utc); SELECT SCOPE_IDENTITY() AS Id;";
                var command = new SqlCommand(sqlText, _connection);
                command.Parameters.Add("@name", SqlDbType.NVarChar, 50).Value = record.Name;
                command.Parameters.Add("@utc", SqlDbType.DateTime).Value = record.Utc;

                object res = await command.ExecuteScalarAsync(cancellationToken);
                record.Id = Convert.ToInt32(res);

                return record.Id;
            }
            catch (Exception ex)
            {
                throw new RepositoryException("InsertGreeting", ex);
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    class RepositoryException : Exception
    {
        internal RepositoryException(string methodName, Exception innerException)
            : base($"Repository Operation `{methodName}` failed", innerException) 
        {}
    }
}
