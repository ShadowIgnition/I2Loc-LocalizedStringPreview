using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps an asynchronous term selection bound to the exact serialized property
/// that opened the picker, even when Unity reuses one drawer for array elements.
/// </summary>
internal sealed class I2TermSelectionTarget
{
	const string TermFieldName = "mTerm";
	const string IgnoreArabicFixFieldName = "mRTL_IgnoreArabicFix";
	const string MaximumRtlLineLengthFieldName = "mRTL_MaxLineLength";
	const string ConvertNumbersFieldName = "mRTL_ConvertNumbers";
	const string DontLocalizeParametersFieldName = "m_DontLocalizeParameters";

	readonly SelectionSnapshot[] m_ExpectedValues;
	readonly string m_PropertyPath;
	readonly UnityEngine.Object[] m_Targets;

	I2TermSelectionTarget(
		UnityEngine.Object[] targets,
		string propertyPath,
		SelectionSnapshot[] expectedValues)
	{
		m_Targets = targets;
		m_PropertyPath = propertyPath;
		m_ExpectedValues = expectedValues;
	}

	internal static bool TryCapture(
		SerializedProperty property,
		out I2TermSelectionTarget selectionTarget)
	{
		selectionTarget = null;
		if (property == null || property.serializedObject == null)
		{
			return false;
		}

		UnityEngine.Object[] sourceTargets = property.serializedObject.targetObjects;
		if (sourceTargets == null || sourceTargets.Length == 0)
		{
			return false;
		}

		UnityEngine.Object[] targets = (UnityEngine.Object[])sourceTargets.Clone();
		SelectionSnapshot[] expectedValues = new SelectionSnapshot[targets.Length];
		for (int i = 0; i < targets.Length; i++)
		{
			UnityEngine.Object target = targets[i];
			if (target == null)
			{
				return false;
			}

			if (!TryReadSnapshot(target, property.propertyPath, out expectedValues[i]))
			{
				return false;
			}
		}

		selectionTarget = new I2TermSelectionTarget(
			targets,
			property.propertyPath,
			expectedValues);
		return true;
	}

	internal bool TryApply(string selectedTerm)
	{
		if (selectedTerm == null)
		{
			return false;
		}

		for (int i = 0; i < m_Targets.Length; i++)
		{
			UnityEngine.Object target = m_Targets[i];
			if (target == null)
			{
				return false;
			}

			SelectionSnapshot currentValue;
			if (!TryReadSnapshot(target, m_PropertyPath, out currentValue) ||
				!currentValue.Equals(m_ExpectedValues[i]))
			{
				return false;
			}
		}

		try
		{
			using (SerializedObject serializedObject = new SerializedObject(m_Targets))
			{
				serializedObject.Update();
				SerializedProperty localizedString = serializedObject.FindProperty(m_PropertyPath);
				SerializedProperty termProperty = localizedString == null
					? null
					: localizedString.FindPropertyRelative(TermFieldName);
				if (termProperty == null || termProperty.propertyType != SerializedPropertyType.String)
				{
					return false;
				}

				Undo.SetCurrentGroupName("Select I2 Localization Term");
				termProperty.stringValue = selectedTerm;
				serializedObject.ApplyModifiedProperties();
				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	static bool TryReadSnapshot(
		UnityEngine.Object target,
		string propertyPath,
		out SelectionSnapshot snapshot)
	{
		snapshot = default(SelectionSnapshot);
		if (target == null || string.IsNullOrEmpty(propertyPath))
		{
			return false;
		}

		try
		{
			using (SerializedObject serializedObject = new SerializedObject(target))
			{
				serializedObject.Update();
				SerializedProperty localizedString = serializedObject.FindProperty(propertyPath);
				SerializedProperty termProperty = localizedString == null
					? null
					: localizedString.FindPropertyRelative(TermFieldName);
				if (termProperty == null || termProperty.propertyType != SerializedPropertyType.String)
				{
					return false;
				}

				bool? ignoreArabicFix;
				int? maximumRtlLineLength;
				bool? convertNumbers;
				bool? dontLocalizeParameters;
				if (!TryReadOptionalBoolean(
						localizedString,
						IgnoreArabicFixFieldName,
						out ignoreArabicFix) ||
					!TryReadOptionalInteger(
						localizedString,
						MaximumRtlLineLengthFieldName,
						out maximumRtlLineLength) ||
					!TryReadOptionalBoolean(
						localizedString,
						ConvertNumbersFieldName,
						out convertNumbers) ||
					!TryReadOptionalBoolean(
						localizedString,
						DontLocalizeParametersFieldName,
						out dontLocalizeParameters))
				{
					return false;
				}

				snapshot = new SelectionSnapshot(
					termProperty.stringValue,
					ignoreArabicFix,
					maximumRtlLineLength,
					convertNumbers,
					dontLocalizeParameters);
				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	static bool TryReadOptionalBoolean(
		SerializedProperty property,
		string relativePropertyPath,
		out bool? value)
	{
		SerializedProperty child = property.FindPropertyRelative(relativePropertyPath);
		if (child == null)
		{
			value = null;
			return true;
		}

		if (child.propertyType != SerializedPropertyType.Boolean)
		{
			value = null;
			return false;
		}

		value = child.boolValue;
		return true;
	}

	static bool TryReadOptionalInteger(
		SerializedProperty property,
		string relativePropertyPath,
		out int? value)
	{
		SerializedProperty child = property.FindPropertyRelative(relativePropertyPath);
		if (child == null)
		{
			value = null;
			return true;
		}

		if (child.propertyType != SerializedPropertyType.Integer)
		{
			value = null;
			return false;
		}

		value = child.intValue;
		return true;
	}

	readonly struct SelectionSnapshot : IEquatable<SelectionSnapshot>
	{
		readonly bool? m_ConvertNumbers;
		readonly bool? m_DontLocalizeParameters;
		readonly bool? m_IgnoreArabicFix;
		readonly int? m_MaximumRtlLineLength;
		readonly string m_Term;

		internal SelectionSnapshot(
			string term,
			bool? ignoreArabicFix,
			int? maximumRtlLineLength,
			bool? convertNumbers,
			bool? dontLocalizeParameters)
		{
			m_Term = term ?? string.Empty;
			m_IgnoreArabicFix = ignoreArabicFix;
			m_MaximumRtlLineLength = maximumRtlLineLength;
			m_ConvertNumbers = convertNumbers;
			m_DontLocalizeParameters = dontLocalizeParameters;
		}

		public bool Equals(SelectionSnapshot other)
		{
			return string.Equals(m_Term, other.m_Term, StringComparison.Ordinal) &&
				m_IgnoreArabicFix == other.m_IgnoreArabicFix &&
				m_MaximumRtlLineLength == other.m_MaximumRtlLineLength &&
				m_ConvertNumbers == other.m_ConvertNumbers &&
				m_DontLocalizeParameters == other.m_DontLocalizeParameters;
		}
	}
}
