﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Web;

namespace Swaggerator.Test
{
	[TestClass]
	public class SerializerTests
	{
		[TestMethod]
		public void CanWriteCompositeType()
		{
			string model = Swaggerator.Serializers.WriteType(typeof(SampleService.CompositeType), new Stack<Type>());
			Assert.IsFalse(string.IsNullOrEmpty(model));

			var obj = JObject.Parse(model);

			Assert.AreEqual("SampleService.CompositeType", obj["id"]);

			var props = obj["properties"] as JObject;
			Assert.IsNotNull(props);
			Assert.IsTrue(props.HasValues);
			Assert.AreEqual(3, props.Count);

			Assert.AreEqual("string", props["StringValue"]["type"]);
			Assert.AreEqual(true, props["BoolValue"]["required"]);
			Assert.AreEqual("array", props["ArrayValue"]["type"]);
		}

		[TestMethod]
		public void CanWriteTypeStack()
		{
			Stack<Type> typeStack = new Stack<Type>();
			typeStack.Push(typeof(SampleService.CompositeType));

			string models = Serializers.WriteModels(typeStack);

			var obj = JObject.Parse(HttpUtility.UrlDecode(models));

			Assert.AreEqual(1, obj.Count);
			Assert.IsNotNull(obj["SampleService.CompositeType"]);
		}

		[TestMethod]
		public void CanWriteContainerProperty()
		{
			string model = Swaggerator.Serializers.WriteType(typeof(SampleService.CompositeType), new Stack<Type>());
			Assert.IsFalse(string.IsNullOrEmpty(model));

			var obj = JObject.Parse(HttpUtility.UrlDecode(model));

			Assert.AreEqual("SampleService.CompositeType", obj["id"].ToString());

			var container = obj["properties"]["ArrayValue"];
			Assert.AreEqual("array", container["type"]);
			Assert.AreEqual("string", container["items"]["$ref"]);
		}

		[TestMethod]
		public void CanWriteOperation()
		{
			
		}
	}
}
