﻿/*
 * Copyright (c) 2014 Digimarc Corporation
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
 * ServiceList.cs : ServiceList model for serialization.
 */


using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Swaggerator.Core.Models.Services
{
	[DataContract]
	public class ServiceList
	{
		public ServiceList()
		{
			apis = new List<Service>();
		}

		[DataMember]
		public string apiVersion { get; set; }
		[DataMember]
		public string swaggerVersion { get; set; }
		[DataMember]
		public List<Service> apis { get; set; }
	}	
}