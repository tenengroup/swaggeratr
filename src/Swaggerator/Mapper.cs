﻿/*
 * Copyright (c) 2013 Digimarc Corporation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Mapper.cs : Methods to find and describe method and model details. 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;
using Swaggerator.Models;
using Swaggerator.Attributes;
using System.Web;

namespace Swaggerator
{
	internal class Mapper
	{
		internal Mapper(IEnumerable<string> hiddenTags)
		{
			HiddenTags = hiddenTags ?? new List<string>();
		}

		internal readonly IEnumerable<string> HiddenTags;

		/// <summary>
		/// Find methods of the supplied type which have WebGet or WebInvoke attributes.
		/// </summary>
		/// <param name="path">Base service path.</param>
		/// <param name="serviceType">The implementation type to search.</param>
		/// <param name="typeStack">Types to be documented in the models section.</param>
		internal IEnumerable<Method> FindMethods(string path, Type serviceType, Stack<Type> typeStack)
		{
			List<Tuple<string, Operation>> operations = new List<Tuple<string, Operation>>();

			//search all interfaces for this type for potential DataContracts, and build a set of operations
			Type[] interfaces = serviceType.GetInterfaces();
			foreach (Type i in interfaces)
			{
				Attribute dc = i.GetCustomAttribute(typeof(ServiceContractAttribute));
				if (dc != null)
				{
					//found a DataContract, now get a service map and inspect the methods for WebGet/WebInvoke
					InterfaceMapping map = serviceType.GetInterfaceMap(i);
					operations.AddRange(GetOperations(map, typeStack));
				}
			}

			List<Method> methods = new List<Method>();

			//go through the discovered Operations, and combine any like Uri's into Methods.
			foreach (Tuple<string, Operation> t in operations)
			{
				Method method = (from m in methods
									  where m.path.Equals(t.Item1)
									  select m).FirstOrDefault();
				if (method == null)
				{
					method = new Method { path = path + t.Item1 };
					methods.Add(method);
				}
				method.operations.Add(t.Item2);
			}

			return methods;
		}

		/// <summary>
		/// Constructs individual operation objects based on the service implementation.
		/// </summary>
		/// <param name="map">Mapping of the service interface & implementation.</param>
		/// <param name="typeStack">Complex types that will need later processing.</param>
		internal IEnumerable<Tuple<string, Operation>> GetOperations(InterfaceMapping map, Stack<Type> typeStack)
		{
			for (int index = 0; index < map.InterfaceMethods.Count(); index++)
			{
				MethodInfo implementation = map.TargetMethods[index];
				MethodInfo declaration = map.InterfaceMethods[index];

				//if the method is marked Hidden anywhere, skip it
				if (implementation.GetCustomAttribute<HiddenAttribute>() != null ||
					 declaration.GetCustomAttribute<HiddenAttribute>() != null) { continue; }

				//if a tag from either implementation or declaration is marked as not visible, skip it
				var methodTags = implementation.GetCustomAttributes<TagAttribute>().Select(t => t.TagName).Concat(declaration.GetCustomAttributes<TagAttribute>().Select(t => t.TagName));
				if (methodTags.Any(HiddenTags.Contains)) { continue; }

				//find the WebGet/Invoke attributes, or skip if neither is present
				WebGetAttribute wg = declaration.GetCustomAttribute<WebGetAttribute>();
				WebInvokeAttribute wi = declaration.GetCustomAttribute<WebInvokeAttribute>();
				if (wg == null && wi == null) { continue; }

				string httpMethod = (wi == null) ? "GET" : wi.Method;
				string uriTemplate = (wi == null) ? wg.UriTemplate ?? "" : wi.UriTemplate ?? "";

				//implementation description overrides interface description
				string description =
					 Helpers.GetCustomAttributeValue<string, OperationNotesAttribute>(implementation, "Notes") ??
					 Helpers.GetCustomAttributeValue<string, OperationNotesAttribute>(declaration, "Notes") ??
					 Helpers.GetCustomAttributeValue<string, DescriptionAttribute>(implementation, "Description") ??
					 Helpers.GetCustomAttributeValue<string, DescriptionAttribute>(declaration, "Description") ??
					 "";

				string summary =
				Helpers.GetCustomAttributeValue<string, OperationSummaryAttribute>(implementation, "Summary") ??
				Helpers.GetCustomAttributeValue<string, OperationSummaryAttribute>(declaration, "Summary") ??
				"";


				Operation operation = new Operation
				{
					httpMethod = httpMethod,
					nickname = declaration.Name + httpMethod,
					type = HttpUtility.HtmlEncode(Helpers.MapSwaggerType(declaration.ReturnType, typeStack)),
					summary = summary,
					notes = description
				};
				if (declaration.ReturnType.IsArray)
				{
					operation.items = new OperationItems
					{
						Ref = Helpers.MapElementType(declaration.ReturnType, typeStack)
					};
				}

				operation.errorResponses.AddRange(GetResponseCodes(map.TargetMethods[index]));
				operation.errorResponses.AddRange(from r in GetResponseCodes(map.InterfaceMethods[index])
															 where !operation.errorResponses.Any(c => c.code.Equals(r.code))
															 select r);

				Uri uri = new Uri("http://base" + uriTemplate);

				//try to map each implementation parameter to the uriTemplate.
				ParameterInfo[] parameters = declaration.GetParameters();
				foreach (ParameterInfo parameter in parameters)
				{
					Parameter parm = new Parameter
					{
						name = parameter.Name,
						allowMultiple = false,
						required = true,
						type = HttpUtility.HtmlEncode(Helpers.MapSwaggerType(parameter.ParameterType, typeStack))
					};

					//path parameters are simple
					if (uri.LocalPath.Contains("{" + parameter.Name + "}"))
					{
						parm.paramType = "path";
						parm.required = true;
					}
					//query parameters require checking and rewriting the name, as the query string name may not match the method signature name
					else if (uri.Query.ToLower().Contains(HttpUtility.UrlEncode("{" + parameter.Name.ToLower() + "}")))
					{
						parm.paramType = "query";
						parm.required = false;
						string name = parameter.Name;
						string paramName = (from p in HttpUtility.ParseQueryString(uri.Query).AllKeys
												  where HttpUtility.ParseQueryString(uri.Query).Get(p).ToLower().Equals("{" + name.ToLower() + "}")
												  select p).First();
						parm.name = paramName;
					}
					//if we couldn't find it in the uri, it must be a body parameter
					else
					{
						parm.paramType = "body";
						parm.required = true;
					}


					var settings = implementation.GetParameters().First(p => p.Position.Equals(parameter.Position)).GetCustomAttribute<ParameterSettings>() ??
						parameter.GetCustomAttribute<Attributes.ParameterSettings>();
					if (settings != null)
					{
						parm.required = settings.IsRequired;
						parm.description = settings.Description ?? parm.description;
						parm.type = settings.UnderlyingType == null ? parm.type :
							Helpers.MapSwaggerType(settings.UnderlyingType, typeStack);
					}

					operation.parameters.Add(parm);
				}

				yield return new Tuple<string, Operation>(uri.LocalPath, operation);
			}
		}

		private IEnumerable<ResponseCode> GetResponseCodes(MethodInfo methodInfo)
		{
			return methodInfo.GetCustomAttributes<ResponseCodeAttribute>().Select(rca => new ResponseCode
			{
				code = rca.Code,
				message = rca.Description
			});
		}
	}
}