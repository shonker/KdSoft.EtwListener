﻿using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Schema.Generation;

namespace KdSoft.EtwEvents.EventSinks {
    [EventSink(nameof(MongoSink))]
    public class MongoSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static MongoSinkFactory() {
            var evtSinkAssembly = Assembly.GetExecutingAssembly();
            var evtSinkDir = Path.GetDirectoryName(evtSinkAssembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Utils.DirectoryResolveAssembly(evtSinkDir!, args);

            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(MongoSinkOptions options, MongoSinkCredentials creds) {
            try {
                MongoUrl connectionUrl;
                MongoCredential credential;
                SslSettings? sslSettings = null;
                if (string.IsNullOrWhiteSpace(creds.CertificateCommonName)) {
                    connectionUrl = options.GetConnectionUrl(creds.User, creds.Password);
                    credential = MongoCredential.CreateCredential(creds.Database, creds.User, creds.Password);
                }
                else {
                    connectionUrl = options.GetConnectionUrl();
                    var cert = Utils.GetCertificate(StoreLocation.CurrentUser, string.Empty, creds.CertificateCommonName);
                    if (cert == null)
                        cert = Utils.GetCertificate(StoreLocation.LocalMachine, string.Empty, creds.CertificateCommonName);
                    if (cert == null)
                        throw new ArgumentException($"Cannot find certificate for common name '{creds.CertificateCommonName}'.");
                    // if provided the user name would have to match the certificate's Subject DN exactly
                    credential = MongoCredential.CreateMongoX509Credential(null);
                    sslSettings = new SslSettings { ClientCertificates = new[] { cert } };
                    sslSettings.CheckCertificateRevocation = false;
                }

                var mcs = MongoClientSettings.FromUrl(connectionUrl);
                mcs.UseTls = true;
                mcs.Credential = credential;
                if (sslSettings != null)
                    mcs.SslSettings = sslSettings;

                var client = new MongoClient(mcs);
                var db = client.GetDatabase(options.Database);
                var coll = db.GetCollection<BsonDocument>(options.Collection);

                var result = new MongoSink(coll, options.EventFilterFields, options.PayloadFilterFields);
                return Task.FromResult((IEventSink)result);
            }
            catch (Exception ex) {
                var errStr = $@"Error in {nameof(MongoSink)} initialization encountered:{Environment.NewLine}{ex.Message}";
                //healthReporter.ReportProblem(errStr, EventFlowContextIdentifiers.Configuration);
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson) {
            var options = JsonSerializer.Deserialize<MongoSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<MongoSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, creds!);
        }

        string GetJsonSchema<T>() {
            var generator = new JSchemaGenerator();
            var schema = generator.Generate(typeof(T));
            return schema.ToString();
        }

        public string GetCredentialsJsonSchema() {
            return GetJsonSchema<MongoSinkCredentials>();
        }

        public string GetOptionsJsonSchema() {
            return GetJsonSchema<MongoSinkOptions>();
        }
    }
}