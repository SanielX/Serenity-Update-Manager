using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
	public class CLRSingleton<T> : CLRScript where T : CLRScript
	{
		/// <summary>
		/// The instance.
		/// </summary>
		protected static T instance;

		/// <summary>
		/// Gets the instance.
		/// </summary>
		/// <value>The instance.</value>
		public static T Instance
		{
			get
			{
				if (instance == null)
				{
					instance = FindObjectOfType<T>();
					if (instance == null)
					{
						GameObject obj = new GameObject
						{
							name = typeof(T).Name
						};
						instance = obj.AddComponent<T>();
						instance.hideFlags = HideFlags.HideAndDontSave;
					}
				}
				return instance;
			}
		}

        /// <summary>
        /// Use this for initialization.
        /// </summary>
        public override void OnAwake()
        {
			if (instance == null)
			{
				instance = this as T;
				this.gameObject.hideFlags = HideFlags.DontSave;
				DontDestroyOnLoad(gameObject);
			}
			else
			{
				Destroy(gameObject);
			}
		}

		public static void Check() { var _ = Instance; }
	}
}
