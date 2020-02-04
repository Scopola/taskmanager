﻿using System;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Portal.Configuration;
using Portal.Helpers;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.UnitTests
{
    public class SessionFileGeneratorTests
    {
        private WorkflowDbContext _dbContext;
        private ISessionFileGenerator _sessionFileGenerator;
        private IOptions<SecretsConfig> _secretsConfig;
        private int ProcessId { get; set; }
        private string UserFullName { get; set; }

        [SetUp]
        public async Task Setup()
        {
            var dbContextOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;

            _dbContext = new WorkflowDbContext(dbContextOptions);

            ProcessId = 123;
            UserFullName = "TestUser";

            _secretsConfig = A.Fake<IOptions<SecretsConfig>>();
            _secretsConfig.Value.HpdServiceName = "ServiceName";

            _sessionFileGenerator = new SessionFileGenerator(_dbContext,
                _secretsConfig);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
        }

        [Test]
        public async Task Test_PopulateSessionFile_UserFullName_That_Does_Not_Exist_Throws_InvalidOperationException()
        {
            UserFullName = "DOES NOT EXIST";
            var hpdUser = new HpdUser()
            {
                HpdUserId = 1,
                AdUsername = "Test User 1",
                HpdUsername = "TestUser1-Caris"
            };
            await _dbContext.HpdUser.AddAsync(hpdUser);
            await _dbContext.SaveChangesAsync();

            Assert.ThrowsAsync(typeof(InvalidOperationException),
                () => _sessionFileGenerator.PopulateSessionFile(
                    ProcessId,
                    UserFullName)
            );
        }

        [Test]
        public async Task Test_PopulateSessionFile_Returns_Valid_Session_File_Contents()
        {
            UserFullName = "Test User 1";
            var hpdUsername = "TestUser1-Caris";
            var hpdUser = new HpdUser()
            {
                HpdUserId = 1,
                AdUsername = UserFullName,
                HpdUsername = hpdUsername
            };
            await _dbContext.HpdUser.AddAsync(hpdUser);
            await _dbContext.SaveChangesAsync();

            var sessionFile = await _sessionFileGenerator.PopulateSessionFile(
                ProcessId,
                UserFullName);

            Assert.IsNotNull(sessionFile);
            Assert.IsNotNull(sessionFile.CarisWorkspace);
            Assert.AreEqual(hpdUsername,
                sessionFile.CarisWorkspace.DataSources.DataSource.SourceParam.USERNAME);
            Assert.AreEqual(hpdUsername,
                sessionFile.CarisWorkspace.DataSources.DataSource.SourceParam.ASSIGNED_USER);
            Assert.AreEqual(_secretsConfig.Value.HpdServiceName,
                sessionFile.CarisWorkspace.DataSources.DataSource.SourceParam.SERVICENAME);
        }
    }
}