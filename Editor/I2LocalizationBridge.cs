using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adapts to I2 Localization without requiring a compile-time assembly reference.
/// I2 is commonly installed into Unity's predefined assemblies, which package
/// assemblies cannot reference directly.
/// </summary>
internal sealed class I2LocalizationBridge
{
	const string LocalizedStringTypeName = "I2.Loc.LocalizedString";
	const string LocalizedStringDrawerTypeName = "I2.Loc.LocalizedStringDrawer";
	const string LocalizationManagerTypeName = "I2.Loc.LocalizationManager";
	const string TermFieldName = "mTerm";

	static readonly FieldInfo DrawerFieldInfo = typeof(PropertyDrawer).GetField(
		"m_FieldInfo",
		BindingFlags.Instance | BindingFlags.NonPublic);

	bool m_Initialized;
	Type m_LocalizedStringType;
	FieldInfo m_TermField;
	PropertyInfo m_CurrentLanguageProperty;
	PropertyDrawer m_LocalizedStringDrawer;

	/// <summary>
	/// Gets the language currently selected by I2, when available.
	/// </summary>
	public string CurrentLanguage
	{
		get
		{
			EnsureInitialized(null);

			try
			{
				return m_CurrentLanguageProperty == null
					? string.Empty
					: m_CurrentLanguageProperty.GetValue(null, null) as string ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}
	}

	/// <summary>
	/// Checks that a property represents I2's LocalizedString type and exposes its term field.
	/// </summary>
	public bool IsLocalizedString(SerializedProperty property, FieldInfo ownerFieldInfo)
	{
		EnsureInitialized(ownerFieldInfo);

		SerializedProperty termProperty = property.FindPropertyRelative(TermFieldName);
		if (termProperty == null || termProperty.propertyType != SerializedPropertyType.String)
		{
			return false;
		}

		if (ownerFieldInfo != null)
		{
			return ownerFieldInfo.FieldType.FullName == LocalizedStringTypeName;
		}

		return m_LocalizedStringType == null
			? property.type == "LocalizedString"
			: property.type == m_LocalizedStringType.Name;
	}

	/// <summary>
	/// Draws I2's native LocalizedString selector, falling back to the serialized term field.
	/// </summary>
	public void OnGUI(Rect position, SerializedProperty property, GUIContent label, FieldInfo ownerFieldInfo)
	{
		EnsureInitialized(ownerFieldInfo);

		if (m_LocalizedStringDrawer != null)
		{
			try
			{
				m_LocalizedStringDrawer.OnGUI(position, property, label);
				return;
			}
			catch
			{
				// Fall through to a plain term field if I2 changes its editor drawer contract.
			}
		}

		SerializedProperty termProperty = property.FindPropertyRelative(TermFieldName);
		if (termProperty != null)
		{
			EditorGUI.PropertyField(position, termProperty, label);
		}
		else
		{
			EditorGUI.LabelField(position, label, new GUIContent("I2 LocalizedString unavailable"));
		}
	}

	/// <summary>
	/// Gets the height requested by I2's native drawer, or one standard line as a fallback.
	/// </summary>
	public float GetPropertyHeight(SerializedProperty property, GUIContent label, FieldInfo ownerFieldInfo)
	{
		EnsureInitialized(ownerFieldInfo);

		if (m_LocalizedStringDrawer != null)
		{
			try
			{
				return Mathf.Max(
					EditorGUIUtility.singleLineHeight,
					m_LocalizedStringDrawer.GetPropertyHeight(property, label));
			}
			catch
			{
				// Fall back when an installed I2 version changes its drawer behavior.
			}
		}

		return EditorGUIUtility.singleLineHeight;
	}

	/// <summary>
	/// Converts a term through I2's own LocalizedString implementation.
	/// </summary>
	public bool TryGetTranslation(string term, out string translation)
	{
		EnsureInitialized(null);
		translation = string.Empty;

		if (m_LocalizedStringType == null || m_TermField == null || string.IsNullOrWhiteSpace(term))
		{
			return string.IsNullOrWhiteSpace(term);
		}

		try
		{
			object localizedString = Activator.CreateInstance(m_LocalizedStringType);
			m_TermField.SetValue(localizedString, term);
			translation = localizedString.ToString() ?? string.Empty;
			return true;
		}
		catch
		{
			return false;
		}
	}

	void EnsureInitialized(FieldInfo ownerFieldInfo)
	{
		if (!m_Initialized)
		{
			m_Initialized = true;
			m_LocalizedStringType = FindType(LocalizedStringTypeName);
			m_TermField = m_LocalizedStringType == null
				? null
				: m_LocalizedStringType.GetField(TermFieldName, BindingFlags.Instance | BindingFlags.Public);

			Type managerType = FindType(LocalizationManagerTypeName);
			m_CurrentLanguageProperty = managerType == null
				? null
				: managerType.GetProperty("CurrentLanguage", BindingFlags.Static | BindingFlags.Public);

			Type drawerType = FindType(LocalizedStringDrawerTypeName);
			if (drawerType != null && typeof(PropertyDrawer).IsAssignableFrom(drawerType))
			{
				try
				{
					m_LocalizedStringDrawer = Activator.CreateInstance(drawerType, true) as PropertyDrawer;
				}
				catch
				{
					m_LocalizedStringDrawer = null;
				}
			}
		}

		if (m_LocalizedStringDrawer != null && DrawerFieldInfo != null && ownerFieldInfo != null)
		{
			try
			{
				DrawerFieldInfo.SetValue(m_LocalizedStringDrawer, ownerFieldInfo);
			}
			catch
			{
				// Older/newer Unity versions can change PropertyDrawer internals; I2 normally
				// does not require fieldInfo, so drawing can continue without it.
			}
		}
	}

	static Type FindType(string fullName)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Type type = assemblies[i].GetType(fullName, false);
			if (type != null)
			{
				return type;
			}
		}

		return null;
	}
}
