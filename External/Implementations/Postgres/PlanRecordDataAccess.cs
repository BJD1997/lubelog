﻿using CarCareTracker.External.Interfaces;
using CarCareTracker.Models;
using Npgsql;
using System.Text.Json;

namespace CarCareTracker.External.Implementations
{
    public class PGPlanRecordDataAccess : IPlanRecordDataAccess
    {
        private NpgsqlDataSource pgDataSource;
        private readonly ILogger<PGPlanRecordDataAccess> _logger;
        private static string tableName = "planrecords";
        public PGPlanRecordDataAccess(IConfiguration config, ILogger<PGPlanRecordDataAccess> logger)
        {
            pgDataSource = NpgsqlDataSource.Create(config["POSTGRES_CONNECTION"]);
            _logger = logger;
            try
            {
                //create table if not exist.
                string initCMD = $"CREATE TABLE IF NOT EXISTS app.{tableName} (id INT GENERATED BY DEFAULT AS IDENTITY primary key, vehicleId INT not null, data jsonb not null)";
                using (var ctext = pgDataSource.CreateCommand(initCMD))
                {
                    ctext.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }
        public List<PlanRecord> GetPlanRecordsByVehicleId(int vehicleId)
        {
            try
            {
                string cmd = $"SELECT data FROM app.{tableName} WHERE vehicleId = @vehicleId";
                var results = new List<PlanRecord>();
                using (var ctext = pgDataSource.CreateCommand(cmd))
                {
                    ctext.Parameters.AddWithValue("vehicleId", vehicleId);
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            PlanRecord planRecord = JsonSerializer.Deserialize<PlanRecord>(reader["data"] as string);
                            results.Add(planRecord);
                        }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new List<PlanRecord>();
            }
        }
        public PlanRecord GetPlanRecordById(int planRecordId)
        {
            try
            {
                string cmd = $"SELECT data FROM app.{tableName} WHERE id = @id";
                var result = new PlanRecord();
                using (var ctext = pgDataSource.CreateCommand(cmd))
                {
                    ctext.Parameters.AddWithValue("id", planRecordId);
                    using (NpgsqlDataReader reader = ctext.ExecuteReader())
                        while (reader.Read())
                        {
                            PlanRecord planRecord = JsonSerializer.Deserialize<PlanRecord>(reader["data"] as string);
                            result = planRecord;
                        }
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new PlanRecord();
            }
        }
        public bool DeletePlanRecordById(int planRecordId)
        {
            try
            {
                string cmd = $"DELETE FROM app.{tableName} WHERE id = @id";
                using (var ctext = pgDataSource.CreateCommand(cmd))
                {
                    ctext.Parameters.AddWithValue("id", planRecordId);
                    return ctext.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
        }
        public bool SavePlanRecordToVehicle(PlanRecord planRecord)
        {
            try
            {
                if (planRecord.Id == default)
                {
                    string cmd = $"INSERT INTO app.{tableName} (vehicleId, data) VALUES(@vehicleId, CAST(@data AS jsonb)) RETURNING id";
                    using (var ctext = pgDataSource.CreateCommand(cmd))
                    {
                        ctext.Parameters.AddWithValue("vehicleId", planRecord.VehicleId);
                        ctext.Parameters.AddWithValue("data", "{}");
                        planRecord.Id = Convert.ToInt32(ctext.ExecuteScalar());
                        //update json data
                        if (planRecord.Id != default)
                        {
                            string cmdU = $"UPDATE app.{tableName} SET data = CAST(@data AS jsonb) WHERE id = @id";
                            using (var ctextU = pgDataSource.CreateCommand(cmdU))
                            {
                                var serializedData = JsonSerializer.Serialize(planRecord);
                                ctextU.Parameters.AddWithValue("id", planRecord.Id);
                                ctextU.Parameters.AddWithValue("data", serializedData);
                                return ctextU.ExecuteNonQuery() > 0;
                            }
                        }
                        return planRecord.Id != default;
                    }
                }
                else
                {
                    string cmd = $"UPDATE app.{tableName} SET data = CAST(@data AS jsonb) WHERE id = @id";
                    using (var ctext = pgDataSource.CreateCommand(cmd))
                    {
                        var serializedData = JsonSerializer.Serialize(planRecord);
                        ctext.Parameters.AddWithValue("id", planRecord.Id);
                        ctext.Parameters.AddWithValue("data", serializedData);
                        return ctext.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
        }
        public bool DeleteAllPlanRecordsByVehicleId(int vehicleId)
        {
            try
            {
                string cmd = $"DELETE FROM app.{tableName} WHERE vehicleId = @id";
                using (var ctext = pgDataSource.CreateCommand(cmd))
                {
                    ctext.Parameters.AddWithValue("id", vehicleId);
                    return ctext.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
        }
    }
}
