using System;
using System.Collections.Generic;
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
	const string LocalizationChangedEventName = "OnLocalizeEvent";
	const string TermFieldName = "mTerm";
	const string IgnoreArabicFixFieldName = "mRTL_IgnoreArabicFix";
	const string MaximumRtlLineLengthFieldName = "mRTL_MaxLineLength";
	const string ConvertNumbersFieldName = "mRTL_ConvertNumbers";
	const string DontLocalizeParametersFieldName = "m_DontLocalizeParameters";

	static readonly FieldInfo DrawerFieldInfo = typeof(PropertyDrawer).GetField(
		"m_FieldInfo",
		BindingFlags.Instance | BindingFlags.NonPublic);

	bool m_Initialized;
	Type m_LocalizedStringType;
	FieldInfo m_TermField;
	FieldInfo m_IgnoreArabicFixField;
	FieldInfo m_MaximumRtlLineLengthField;
	FieldInfo m_ConvertNumbersField;
	FieldInfo m_DontLocalizeParametersField;
	PropertyInfo m_CurrentLanguageProperty;
	EventInfo m_LocalizationChangedEvent;
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

		I2LocalizedStringData data;
		bool hasMultipleDifferentValues;
		if (!TryReadData(property, out data, out hasMultipleDifferentValues))
		{
			return false;
		}

		if (ownerFieldInfo != null)
		{
			Type ownerFieldType = UnwrapCollectionType(ownerFieldInfo.FieldType);
			return ownerFieldType != null && ownerFieldType.FullName == LocalizedStringTypeName;
		}

		return m_LocalizedStringType == null
			? property.type == "LocalizedString"
			: property.type == m_LocalizedStringType.Name;
	}

	/// <summary>
	/// Subscribes to I2's global localization refresh callback when the installed
	/// version exposes it. Unity project events remain the fallback invalidation path.
	/// </summary>
	public bool TrySubscribeToLocalizationChanges(Action callback)
	{
		EnsureInitialized(null);
		if (callback == null || m_LocalizationChangedEvent == null)
		{
			return false;
		}

		try
		{
			Delegate handler = Delegate.CreateDelegate(
				m_LocalizationChangedEvent.EventHandlerType,
				callback.Target,
				callback.Method);
			m_LocalizationChangedEvent.AddEventHandler(null, handler);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Reads the complete serialized value needed to mirror I2's runtime conversion.
	/// </summary>
	public bool TryReadData(
		SerializedProperty property,
		out I2LocalizedStringData data,
		out bool hasMultipleDifferentValues)
	{
		SerializedProperty termProperty = property.FindPropertyRelative(TermFieldName);
		SerializedProperty ignoreArabicFixProperty =
			property.FindPropertyRelative(IgnoreArabicFixFieldName);
		SerializedProperty maximumRtlLineLengthProperty =
			property.FindPropertyRelative(MaximumRtlLineLengthFieldName);
		SerializedProperty convertNumbersProperty =
			property.FindPropertyRelative(ConvertNumbersFieldName);
		SerializedProperty dontLocalizeParametersProperty =
			property.FindPropertyRelative(DontLocalizeParametersFieldName);

		bool hasRequiredLayout =
			termProperty != null &&
			termProperty.propertyType == SerializedPropertyType.String &&
			ignoreArabicFixProperty != null &&
			ignoreArabicFixProperty.propertyType == SerializedPropertyType.Boolean &&
			maximumRtlLineLengthProperty != null &&
			maximumRtlLineLengthProperty.propertyType == SerializedPropertyType.Integer &&
			convertNumbersProperty != null &&
			convertNumbersProperty.propertyType == SerializedPropertyType.Boolean;

		if (!hasRequiredLayout ||
			(dontLocalizeParametersProperty != null &&
			 dontLocalizeParametersProperty.propertyType != SerializedPropertyType.Boolean))
		{
			data = default(I2LocalizedStringData);
			hasMultipleDifferentValues = false;
			return false;
		}

		hasMultipleDifferentValues =
			property.hasMultipleDifferentValues ||
			termProperty.hasMultipleDifferentValues ||
			ignoreArabicFixProperty.hasMultipleDifferentValues ||
			maximumRtlLineLengthProperty.hasMultipleDifferentValues ||
			convertNumbersProperty.hasMultipleDifferentValues ||
			(dontLocalizeParametersProperty != null &&
			 dontLocalizeParametersProperty.hasMultipleDifferentValues);

		data = new I2LocalizedStringData(
			termProperty.stringValue,
			ignoreArabicFixProperty.boolValue,
			maximumRtlLineLengthProperty.intValue,
			convertNumbersProperty.boolValue,
			dontLocalizeParametersProperty != null && dontLocalizeParametersProperty.boolValue);
		return true;
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
	public bool TryGetTranslation(I2LocalizedStringData data, out string translation)
	{
		EnsureInitialized(null);
		translation = string.Empty;

		if (string.IsNullOrWhiteSpace(data.Term))
		{
			return true;
		}

		if (m_LocalizedStringType == null ||
			m_TermField == null ||
			m_IgnoreArabicFixField == null ||
			m_MaximumRtlLineLengthField == null ||
			m_ConvertNumbersField == null)
		{
			return false;
		}

		try
		{
			object localizedString = Activator.CreateInstance(m_LocalizedStringType);
			m_TermField.SetValue(localizedString, data.Term);
			m_IgnoreArabicFixField.SetValue(localizedString, data.IgnoreArabicFix);
			m_MaximumRtlLineLengthField.SetValue(localizedString, data.MaximumRtlLineLength);
			m_ConvertNumbersField.SetValue(localizedString, data.ConvertNumbers);

			if (m_DontLocalizeParametersField != null)
			{
				m_DontLocalizeParametersField.SetValue(localizedString, data.DontLocalizeParameters);
			}

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
				: GetInstanceField(m_LocalizedStringType, TermFieldName);
			m_IgnoreArabicFixField = m_LocalizedStringType == null
				? null
				: GetInstanceField(m_LocalizedStringType, IgnoreArabicFixFieldName);
			m_MaximumRtlLineLengthField = m_LocalizedStringType == null
				? null
				: GetInstanceField(m_LocalizedStringType, MaximumRtlLineLengthFieldName);
			m_ConvertNumbersField = m_LocalizedStringType == null
				? null
				: GetInstanceField(m_LocalizedStringType, ConvertNumbersFieldName);
			m_DontLocalizeParametersField = m_LocalizedStringType == null
				? null
				: GetInstanceField(m_LocalizedStringType, DontLocalizeParametersFieldName);

			Type managerType = FindType(LocalizationManagerTypeName);
			m_CurrentLanguageProperty = managerType == null
				? null
				: managerType.GetProperty("CurrentLanguage", BindingFlags.Static | BindingFlags.Public);
			m_LocalizationChangedEvent = managerType == null
				? null
				: managerType.GetEvent(
					LocalizationChangedEventName,
					BindingFlags.Static | BindingFlags.Public);

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

	static FieldInfo GetInstanceField(Type type, string fieldName)
	{
		return type.GetField(
			fieldName,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	static Type UnwrapCollectionType(Type type)
	{
		if (type.IsArray)
		{
			return type.GetElementType();
		}

		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
		{
			return type.GetGenericArguments()[0];
		}

		return type;
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

internal readonly struct I2LocalizedStringData : IEquatable<I2LocalizedStringData>
{
	internal I2LocalizedStringData(
		string term,
		bool ignoreArabicFix,
		int maximumRtlLineLength,
		bool convertNumbers,
		bool dontLocalizeParameters)
	{
		Term = term ?? string.Empty;
		IgnoreArabicFix = ignoreArabicFix;
		MaximumRtlLineLength = maximumRtlLineLength;
		ConvertNumbers = convertNumbers;
		DontLocalizeParameters = dontLocalizeParameters;
	}

	internal string Term { get; }
	internal bool IgnoreArabicFix { get; }
	internal int MaximumRtlLineLength { get; }
	internal bool ConvertNumbers { get; }
	internal bool DontLocalizeParameters { get; }

	public bool Equals(I2LocalizedStringData other)
	{
		return string.Equals(Term, other.Term, StringComparison.Ordinal) &&
			IgnoreArabicFix == other.IgnoreArabicFix &&
			MaximumRtlLineLength == other.MaximumRtlLineLength &&
			ConvertNumbers == other.ConvertNumbers &&
			DontLocalizeParameters == other.DontLocalizeParameters;
	}

	public override bool Equals(object obj)
	{
		return obj is I2LocalizedStringData && Equals((I2LocalizedStringData)obj);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			int hashCode = Term == null ? 0 : Term.GetHashCode();
			hashCode = (hashCode * 397) ^ IgnoreArabicFix.GetHashCode();
			hashCode = (hashCode * 397) ^ MaximumRtlLineLength;
			hashCode = (hashCode * 397) ^ ConvertNumbers.GetHashCode();
			hashCode = (hashCode * 397) ^ DontLocalizeParameters.GetHashCode();
			return hashCode;
		}
	}
}
