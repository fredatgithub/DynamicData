﻿using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class IncludeUpdateFixture
    {
        private ISourceCache<Person, string> _source;
        private TestChangeSetResult<Person, string> _results;

        [SetUp]
        public void MyTestInitialize()
        {
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = new TestChangeSetResult<Person, string>
                (
                _source.Connect().IncludeUpdateWhen((current, previous) => current != previous)
                );
        }

        [Test]
        public void IgnoreFunctionWillIgnoreSubsequentUpdatesOfAnItem()
        {
            var person = new Person("Person", 10);
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

    }
}