﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheTower.Extensions.Redis;
using CacheTower.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CacheTower.Tests.Extensions.Redis
{
	[TestClass]
	public class RedisRemoteEvictionExtensionTests
	{
		[TestMethod, ExpectedException(typeof(ArgumentNullException))]
		public void ThrowForNullConnection()
		{
			new RedisRemoteEvictionExtension(null);
		}

		[TestMethod, ExpectedException(typeof(ArgumentNullException))]
		public void ThrowForNullChannel()
		{
			new RedisRemoteEvictionExtension(RedisHelper.GetConnection(), null);
		}

		[TestMethod, ExpectedException(typeof(InvalidOperationException))]
		public void ThrowForRegisteringMoreThanOneCacheStack()
		{
			var extension = new RedisRemoteEvictionExtension(RedisHelper.GetConnection());
			var cacheStackMock = new Mock<ICacheStack>();
			extension.Register(cacheStackMock.Object);
			extension.Register(cacheStackMock.Object);
		}

		[TestMethod]
		public async Task EvictsFromChannelButNotFromRegisteredCacheStack()
		{
			var connection = RedisHelper.GetConnection();
			var completionSource = new TaskCompletionSource<bool>();

			await connection.GetSubscriber().SubscribeAsync("CacheTower.RemoteEviction", (channel, value) =>
			{
				if (value == "TestKey")
				{
					completionSource.SetResult(true);
				}
			});

			var cacheStackMock = new Mock<ICacheStack>();
			var extension = new RedisRemoteEvictionExtension(connection);
			extension.Register(cacheStackMock.Object);

			await extension.OnValueRefreshAsync("TestKey", TimeSpan.FromDays(1));

			var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromSeconds(30)));

			Assert.AreEqual(completionSource.Task, completedTask, "Subscribers were not notified about the refreshed value within the time limit");
			cacheStackMock.Verify(c => c.EvictAsync("TestKey"), Times.Never, "The CacheStack that published the refresh was told to evict its own cache");
		}
	}
}
