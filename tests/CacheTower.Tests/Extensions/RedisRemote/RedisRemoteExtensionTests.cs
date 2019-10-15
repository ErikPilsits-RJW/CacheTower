﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheTower.Extensions.RedisRemote;
using CacheTower.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CacheTower.Tests.Extensions.RedisRemote
{
	[TestClass, Ignore]
	public class RedisRemoteExtensionTests
	{
		[TestMethod, ExpectedException(typeof(ArgumentNullException))]
		public void ThrowForNullConnection()
		{
			new RedisRemoteExtension(null);
		}

		[TestMethod, ExpectedException(typeof(ArgumentNullException))]
		public void ThrowForNullChannel()
		{
			new RedisRemoteExtension(RedisHelper.GetConnection(), null);
		}


		[TestMethod]
		public async Task EvictsFromChannelButNotFromRegisteredCacheStack()
		{
			var connection = RedisHelper.GetConnection();
			var hasMessagedSubscribers = false;

			await connection.GetSubscriber().SubscribeAsync("CacheTower.ValueRefresh", (channel, value) =>
			{
				if (value == "TestKey")
				{
					hasMessagedSubscribers = true;
				}
			});

			var cacheStackMock = new Mock<ICacheStack>();
			var extension = new RedisRemoteExtension(connection);
			extension.Register(cacheStackMock.Object);

			await extension.OnValueRefreshAsync(cacheStackMock.Object, "TestKey", TimeSpan.FromDays(1));
			await Task.Delay(1000);

			Assert.IsTrue(hasMessagedSubscribers, "Subscribers were not notified about the refreshed value");
			cacheStackMock.Verify(c => c.EvictAsync("TestKey"), Times.Never, "The CacheStack that published the refresh was told to evict its own cache");
		}
	}
}