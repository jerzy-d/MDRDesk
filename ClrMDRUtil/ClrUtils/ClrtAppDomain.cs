using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace ClrMDRIndex
{
	public class ClrtAppDomains
	{
		public enum AppDomainIds
		{
			SystemDomain = Int32.MaxValue, 
			SharedDoamin = Int32.MaxValue-1, 
			AppDomain = 0
		}

		ClrtAppDomain _systemDomain;
		ClrtAppDomain _sharedDomain;
		ClrtAppDomain[] _appDomains;

		public ClrtAppDomain SystemDomain => _systemDomain;
		public ClrtAppDomain SharedDomain => _sharedDomain;
		public int AppDomainCount => _appDomains.Length;

		public ClrtAppDomains(ClrtAppDomain systemDomain, ClrtAppDomain sharedDomain)
		{
			_systemDomain = systemDomain;
			_sharedDomain = sharedDomain;
		}

		public void AddAppDomains(ClrtAppDomain[] domains)
		{
			_appDomains = domains;
		}

		public ClrtAppDomain this[int ndx]
		{
			get { return ndx < 0 || ndx >=_appDomains.Length ? null :_appDomains[ndx]; }
		}

		public int FindAppDomain(ClrAppDomain domain)
		{
			for (int i = 0, icnt = _appDomains.Length; i < icnt; ++i)
			{
				if (_appDomains[i].Address == domain.Address) return i;
			}
			if (_systemDomain.Address == domain.Address) return (int)AppDomainIds.SystemDomain;
			if (_sharedDomain.Address == domain.Address) return (int)AppDomainIds.SharedDoamin;
			return Constants.InvalidIndex;
		}

	}

	public class ClrtAppDomain
	{
		/// <summary>
		/// Address of the AppDomain.
		/// </summary>
		public ulong Address { get; }

		/// <summary>
		/// The AppDomain's ID.
		/// </summary>
		public int Id { get; }

		/// <summary>
		/// The name of the AppDomain, as specified when the domain was created.
		/// </summary>
		public int NameId { get; }

		/// <summary>
		/// Returns the config file used for the AppDomain.  This may be null if there was no config file
		/// loaded, or if the targeted runtime does not support enumerating that data.
		/// </summary>
		public int ConfigurationFileId { get; }

		/// <summary>
		/// Returns the base directory for this AppDomain.  This may return null if the targeted runtime does
		/// not support enumerating this information.
		/// </summary>
		public int ApplicationBaseId { get; }

		public ClrtAppDomain()
		{
			Address = Constants.InvalidAddress;
			Id = Constants.InvalidIndex;
			NameId = Constants.InvalidIndex;
			ConfigurationFileId = Constants.InvalidIndex;
			ApplicationBaseId = Constants.InvalidIndex;
		}

		public ClrtAppDomain(ClrAppDomain appDomain, StringIdAsyncDct dct)
		{
			Address = appDomain.Address;
			Id = appDomain.Id;
			NameId = dct.JustGetId(appDomain.Name);
			ConfigurationFileId = dct.JustGetId(appDomain.ConfigurationFile);
			ApplicationBaseId = dct.JustGetId(appDomain.ApplicationBase);
		}
	}
}
