﻿using CarCareTracker.External.Implementations;
using CarCareTracker.Helper;
using CarCareTracker.Models;
using LiteDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data.Common;
using System.IO.Compression;
using System.Xml.Linq;

namespace CarCareTracker.Controllers
{
    [Authorize(Roles = nameof(UserData.IsRootUser))]
    public class MigrationController : Controller
    {
        private IConfigHelper _configHelper;
        private IFileHelper _fileHelper;
        private readonly ILogger<MigrationController> _logger;
        public MigrationController(IConfigHelper configHelper, IFileHelper fileHelper, IConfiguration serverConfig, ILogger<MigrationController> logger)
        {
            _configHelper = configHelper;
            _fileHelper = fileHelper;
            _logger = logger;
        }
        public IActionResult Index()
        {
            if (!string.IsNullOrWhiteSpace(_configHelper.GetServerPostgresConnection()))
            {
                return View();
            } else
            {
                return new RedirectResult("/Error/Unauthorized");
            }
        }
        private void InitializeTables(NpgsqlConnection conn)
        {
            var cmds = new List<string>
            {
                "CREATE TABLE IF NOT EXISTS app.vehicles (id INT GENERATED BY DEFAULT AS IDENTITY primary key, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.collisionrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.upgraderecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.servicerecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.gasrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.notes (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.odometerrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.reminderrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.planrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.planrecordtemplates (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.supplyrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.taxrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.userrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, username TEXT not null, emailaddress TEXT not null, password TEXT not null, isadmin BOOLEAN)",
                "CREATE TABLE IF NOT EXISTS app.tokenrecords (id INT GENERATED BY DEFAULT AS IDENTITY primary key, body TEXT not null, emailaddress TEXT not null)",
                "CREATE TABLE IF NOT EXISTS app.userconfigrecords (id INT primary key, data jsonb not null)",
                "CREATE TABLE IF NOT EXISTS app.useraccessrecords (userId INT, vehicleId INT, PRIMARY KEY(userId, vehicleId))"
            };
            foreach(string cmd in cmds)
            {
                using (var ctext = new NpgsqlCommand(cmd, conn))
                {
                    ctext.ExecuteNonQuery();
                }
            }
        }
        public IActionResult Export()
        {
            if (string.IsNullOrWhiteSpace(_configHelper.GetServerPostgresConnection()))
            {
                return Json(new OperationResponse { Success = false, Message = "Postgres connection not set up" });
            }
            var tempFolder = $"temp/{Guid.NewGuid()}";
            var tempPath = $"{tempFolder}/cartracker.db";
            var fullFolderPath = _fileHelper.GetFullFilePath(tempFolder, false);
            Directory.CreateDirectory(fullFolderPath);
            var fullFileName = _fileHelper.GetFullFilePath(tempPath, false);
            try
            {
                var pgDataSource = new NpgsqlConnection(_configHelper.GetServerPostgresConnection());
                if (pgDataSource.State == System.Data.ConnectionState.Closed)
                {
                    pgDataSource.Open();
                }
                InitializeTables(pgDataSource);
                //pull records
                var vehicles = new List<Vehicle>();
                var repairrecords = new List<CollisionRecord>();
                var upgraderecords = new List<UpgradeRecord>();
                var servicerecords = new List<ServiceRecord>();

                var gasrecords = new List<GasRecord>();
                var noterecords = new List<Note>();
                var odometerrecords = new List<OdometerRecord>();
                var reminderrecords = new List<ReminderRecord>();

                var planrecords = new List<PlanRecord>();
                var planrecordtemplates = new List<PlanRecordInput>();
                var supplyrecords = new List<SupplyRecord>();
                var taxrecords = new List<TaxRecord>();

                var userrecords = new List<UserData>();
                var tokenrecords = new List<Token>();
                var userconfigrecords = new List<UserConfigData>();
                var useraccessrecords = new List<UserAccess>();
                #region "Part1"
                string cmd = $"SELECT data FROM app.vehicles";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            Vehicle vehicle = System.Text.Json.JsonSerializer.Deserialize<Vehicle>(reader["data"] as string);
                            vehicles.Add(vehicle);
                        }
                }
                foreach (var vehicle in vehicles)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<Vehicle>("vehicles");
                        table.Upsert(vehicle);
                    };
                }
                cmd = $"SELECT data FROM app.collisionrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            repairrecords.Add(System.Text.Json.JsonSerializer.Deserialize<CollisionRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in repairrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<CollisionRecord>("collisionrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.upgraderecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            upgraderecords.Add(System.Text.Json.JsonSerializer.Deserialize<UpgradeRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in upgraderecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<UpgradeRecord>("upgraderecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.servicerecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            servicerecords.Add(System.Text.Json.JsonSerializer.Deserialize<ServiceRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in servicerecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<ServiceRecord>("servicerecords");
                        table.Upsert(record);
                    };
                }
                #endregion
                #region "Part2"
                cmd = $"SELECT data FROM app.gasrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            gasrecords.Add(System.Text.Json.JsonSerializer.Deserialize<GasRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in gasrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<GasRecord>("gasrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.notes";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            noterecords.Add(System.Text.Json.JsonSerializer.Deserialize<Note>(reader["data"] as string));
                        }
                }
                foreach (var record in noterecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<Note>("notes");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.odometerrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            odometerrecords.Add(System.Text.Json.JsonSerializer.Deserialize<OdometerRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in odometerrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<OdometerRecord>("odometerrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.reminderrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            reminderrecords.Add(System.Text.Json.JsonSerializer.Deserialize<ReminderRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in reminderrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<ReminderRecord>("reminderrecords");
                        table.Upsert(record);
                    };
                }
                #endregion
                #region "Part3"
                cmd = $"SELECT data FROM app.planrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            planrecords.Add(System.Text.Json.JsonSerializer.Deserialize<PlanRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in planrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<PlanRecord>("planrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.planrecordtemplates";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            planrecordtemplates.Add(System.Text.Json.JsonSerializer.Deserialize<PlanRecordInput>(reader["data"] as string));
                        }
                }
                foreach (var record in planrecordtemplates)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<PlanRecordInput>("planrecordtemplates");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.supplyrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            supplyrecords.Add(System.Text.Json.JsonSerializer.Deserialize<SupplyRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in supplyrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<SupplyRecord>("supplyrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.taxrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            taxrecords.Add(System.Text.Json.JsonSerializer.Deserialize<TaxRecord>(reader["data"] as string));
                        }
                }
                foreach (var record in taxrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<TaxRecord>("taxrecords");
                        table.Upsert(record);
                    };
                }
                #endregion
                #region "Part4"
                cmd = $"SELECT id, username, emailaddress, password, isadmin FROM app.userrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            UserData result = new UserData();
                            result.Id = int.Parse(reader["id"].ToString());
                            result.UserName = reader["username"].ToString();
                            result.EmailAddress = reader["emailaddress"].ToString();
                            result.Password = reader["password"].ToString();
                            result.IsAdmin = bool.Parse(reader["isadmin"].ToString());
                            userrecords.Add(result);
                        }
                }
                foreach (var record in userrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<UserData>("userrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT id, emailaddress, body FROM app.tokenrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            Token result = new Token();
                            result.Id = int.Parse(reader["id"].ToString());
                            result.EmailAddress = reader["emailaddress"].ToString();
                            result.Body = reader["body"].ToString();
                            tokenrecords.Add(result);
                        }
                }
                foreach (var record in tokenrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<Token>("tokenrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT data FROM app.userconfigrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            userconfigrecords.Add(System.Text.Json.JsonSerializer.Deserialize<UserConfigData>(reader["data"] as string));
                        }
                }
                foreach (var record in userconfigrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<UserConfigData>("userconfigrecords");
                        table.Upsert(record);
                    };
                }
                cmd = $"SELECT userId, vehicleId FROM app.useraccessrecords";
                using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                {
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            UserAccess result = new UserAccess()
                            {
                                Id = new UserVehicle
                                {
                                    UserId = int.Parse(reader["userId"].ToString()),
                                    VehicleId = int.Parse(reader["vehicleId"].ToString())
                                }
                            };
                            useraccessrecords.Add(result);
                        }
                }
                foreach (var record in useraccessrecords)
                {
                    using (var db = new LiteDatabase(fullFileName))
                    {
                        var table = db.GetCollection<UserAccess>("useraccessrecords");
                        table.Upsert(record);
                    };
                }
                #endregion
                var destFilePath = $"{fullFolderPath}.zip";
                ZipFile.CreateFromDirectory(fullFolderPath, destFilePath);
                return Json(new OperationResponse { Success = true, Message = $"/{tempFolder}.zip" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new OperationResponse { Success = false, Message = StaticHelper.GenericErrorMessage });
            }
        }
        public IActionResult Import(string fileName)
        {
            if (string.IsNullOrWhiteSpace(_configHelper.GetServerPostgresConnection()))
            {
                return Json(new OperationResponse { Success = false, Message = "Postgres connection not set up" });
            }
            var fullFileName = _fileHelper.GetFullFilePath(fileName);
            if (string.IsNullOrWhiteSpace(fullFileName))
            {
                return Json(new OperationResponse { Success = false, Message = StaticHelper.GenericErrorMessage });
            }
            try
            {
                var pgDataSource = new NpgsqlConnection(_configHelper.GetServerPostgresConnection());
                if (pgDataSource.State == System.Data.ConnectionState.Closed)
                {
                    pgDataSource.Open();
                }
                InitializeTables(pgDataSource);
                //pull records
                var vehicles = new List<Vehicle>();
                var repairrecords = new List<CollisionRecord>();
                var upgraderecords = new List<UpgradeRecord>();
                var servicerecords = new List<ServiceRecord>();

                var gasrecords = new List<GasRecord>();
                var noterecords = new List<Note>();
                var odometerrecords = new List<OdometerRecord>();
                var reminderrecords = new List<ReminderRecord>();

                var planrecords = new List<PlanRecord>();
                var planrecordtemplates = new List<PlanRecordInput>();
                var supplyrecords = new List<SupplyRecord>();
                var taxrecords = new List<TaxRecord>();

                var userrecords = new List<UserData>();
                var tokenrecords = new List<Token>();
                var userconfigrecords = new List<UserConfigData>();
                var useraccessrecords = new List<UserAccess>();
                #region "Part1"
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<Vehicle>("vehicles");
                    vehicles = table.FindAll().ToList();
                };
                foreach(var vehicle in vehicles)
                {
                    string cmd = $"INSERT INTO app.vehicles (id, data) VALUES(@id, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", vehicle.Id);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(vehicle));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<CollisionRecord>("collisionrecords");
                    repairrecords = table.FindAll().ToList();
                };
                foreach (var record in repairrecords)
                {
                    string cmd = $"INSERT INTO app.collisionrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<ServiceRecord>("servicerecords");
                    servicerecords = table.FindAll().ToList();
                };
                foreach (var record in servicerecords)
                {
                    string cmd = $"INSERT INTO app.servicerecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<UpgradeRecord>("upgraderecords");
                    upgraderecords = table.FindAll().ToList();
                };
                foreach (var record in upgraderecords)
                {
                    string cmd = $"INSERT INTO app.upgraderecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                #endregion
                #region "Part2"
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<GasRecord>("gasrecords");
                    gasrecords = table.FindAll().ToList();
                };
                foreach (var record in gasrecords)
                {
                    string cmd = $"INSERT INTO app.gasrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<Note>("notes");
                    noterecords = table.FindAll().ToList();
                };
                foreach (var record in noterecords)
                {
                    string cmd = $"INSERT INTO app.notes (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<OdometerRecord>("odometerrecords");
                    odometerrecords = table.FindAll().ToList();
                };
                foreach (var record in odometerrecords)
                {
                    string cmd = $"INSERT INTO app.odometerrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<ReminderRecord>("reminderrecords");
                    reminderrecords = table.FindAll().ToList();
                };
                foreach (var record in reminderrecords)
                {
                    string cmd = $"INSERT INTO app.reminderrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                #endregion
                #region "Part3"
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<PlanRecord>("planrecords");
                    planrecords = table.FindAll().ToList();
                };
                foreach (var record in planrecords)
                {
                    string cmd = $"INSERT INTO app.planrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<PlanRecordInput>("planrecordtemplates");
                    planrecordtemplates = table.FindAll().ToList();
                };
                foreach (var record in planrecordtemplates)
                {
                    string cmd = $"INSERT INTO app.planrecordtemplates (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<SupplyRecord>("supplyrecords");
                    supplyrecords = table.FindAll().ToList();
                };
                foreach (var record in supplyrecords)
                {
                    string cmd = $"INSERT INTO app.supplyrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<TaxRecord>("taxrecords");
                    taxrecords = table.FindAll().ToList();
                };
                foreach (var record in taxrecords)
                {
                    string cmd = $"INSERT INTO app.taxrecords (id, vehicleId, data) VALUES(@id, @vehicleId, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("vehicleId", record.VehicleId);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                #endregion
                #region "Part4"
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<UserData>("userrecords");
                    userrecords =  table.FindAll().ToList();
                };
                foreach (var record in userrecords)
                {
                    string cmd = $"INSERT INTO app.userrecords (id, username, emailaddress, password, isadmin) VALUES(@id, @username, @emailaddress, @password, @isadmin)";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("username", record.UserName);
                        ctext.Parameters.AddWithValue("emailaddress", record.EmailAddress);
                        ctext.Parameters.AddWithValue("password", record.Password);
                        ctext.Parameters.AddWithValue("isadmin", record.IsAdmin);
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<Token>("tokenrecords");
                    tokenrecords = table.FindAll().ToList();
                };
                foreach (var record in tokenrecords)
                {
                    string cmd = $"INSERT INTO app.tokenrecords (id, emailaddress, body) VALUES(@id, @emailaddress, @body)";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("emailaddress", record.EmailAddress);
                        ctext.Parameters.AddWithValue("body", record.Body);
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<UserConfigData>("userconfigrecords");
                    userconfigrecords = table.FindAll().ToList();
                };
                foreach (var record in userconfigrecords)
                {
                    string cmd = $"INSERT INTO app.userconfigrecords (id, data) VALUES(@id, CAST(@data AS jsonb))";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("id", record.Id);
                        ctext.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(record));
                        ctext.ExecuteNonQuery();
                    }
                }
                using (var db = new LiteDatabase(fullFileName))
                {
                    var table = db.GetCollection<UserAccess>("useraccessrecords");
                    useraccessrecords = table.FindAll().ToList();
                };
                foreach (var record in useraccessrecords)
                {
                    string cmd = $"INSERT INTO app.useraccessrecords (userId, vehicleId) VALUES(@userId, @vehicleId)";
                    using (var ctext = new NpgsqlCommand(cmd, pgDataSource))
                    {
                        ctext.Parameters.AddWithValue("userId", record.Id.UserId);
                        ctext.Parameters.AddWithValue("vehicleId", record.Id.VehicleId);
                        ctext.ExecuteNonQuery();
                    }
                }
                #endregion
                return Json(new OperationResponse { Success = true, Message = "Data Imported Successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return Json(new OperationResponse { Success = false, Message = StaticHelper.GenericErrorMessage });
            }
        }
    }
}
