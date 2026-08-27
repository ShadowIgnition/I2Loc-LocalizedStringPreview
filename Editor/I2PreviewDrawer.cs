// https://github.com/ShadowIgnition/I2Loc-LocalizedStringPreview
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(I2PreviewAttribute))]
public class I2PreviewDrawer : PropertyDrawer
{
	const string RelativeTermPropertyPath = "mTerm";
	const string InvalidPropertyMessage =
		"I2Preview requires an I2.Loc.LocalizedString field with valid serialized data.";
	const int MaximumCachedPropertyCount = 512;
	const float EstimatedInspectorPadding = 40f;

	static readonly GUIContent InvalidPropertyContent = new GUIContent(InvalidPropertyMessage);

	readonly I2LocalizationBridge m_Bridge = new I2LocalizationBridge();
	readonly Dictionary<string, PreviewState> m_States =
		new Dictionary<string, PreviewState>();

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		PreviewState state = GetState(property);
		state.LastKnownWidth = position.width;

		if (!IsValidType(property))
		{
			EditorGUI.HelpBox(position, InvalidPropertyMessage, MessageType.Error);
			return;
		}

		float baseHeight = m_Bridge.GetPropertyHeight(property, label, fieldInfo);
		Rect baseRect = new Rect(
			position.x,
			position.y,
			position.width,
			Mathf.Min(baseHeight, position.height));
		m_Bridge.OnGUI(baseRect, property, label, fieldInfo);

		float previewY = baseRect.yMax + EditorGUIUtility.standardVerticalSpacing;
		float allocatedPreviewHeight = Mathf.Max(0f, position.yMax - previewY);
		if (allocatedPreviewHeight <= 0f)
		{
			return;
		}

		state.SetTranslation(GetTranslation(property));

		GUIStyle style = EditorStyles.textArea;
		float lineHeight = GetLineHeight(style);
		Func<float, float> measureHeight = delegate(float width)
		{
			return style.CalcHeight(state.Content, width) + (lineHeight / 2f);
		};

		float desiredPreviewHeight = I2PreviewLayout.GetDesiredViewportHeight(
			measureHeight,
			position.width,
			lineHeight,
			RequestedLineCount);

		if (!Mathf.Approximately(state.DesiredPreviewHeight, desiredPreviewHeight))
		{
			state.DesiredPreviewHeight = desiredPreviewHeight;
		}

		Rect previewRect = new Rect(
			position.x,
			previewY,
			position.width,
			allocatedPreviewHeight);
		I2PreviewContentLayout contentLayout = I2PreviewLayout.GetContentLayout(
			measureHeight,
			previewRect.width,
			previewRect.height,
			lineHeight,
			GetVerticalScrollbarWidth());

		DrawPreview(previewRect, contentLayout, state, style);
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		if (!IsValidType(property))
		{
			return GetInvalidPropertyHeight(property);
		}

		PreviewState state = GetState(property);
		state.SetTranslation(GetTranslation(property));

		GUIStyle style = EditorStyles.textArea;
		float lineHeight = GetLineHeight(style);
		float availableWidth = state.LastKnownWidth > 0f
			? state.LastKnownWidth
			: GetEstimatedInspectorWidth();
		Func<float, float> measureHeight = delegate(float width)
		{
			return style.CalcHeight(state.Content, width) + (lineHeight / 2f);
		};

		state.DesiredPreviewHeight = I2PreviewLayout.GetDesiredViewportHeight(
			measureHeight,
			availableWidth,
			lineHeight,
			RequestedLineCount);

		return m_Bridge.GetPropertyHeight(property, label, fieldInfo)
			+ EditorGUIUtility.standardVerticalSpacing
			+ state.DesiredPreviewHeight;
	}

	public override bool CanCacheInspectorGUI(SerializedProperty property)
	{
		return false;
	}

	bool IsValidType(SerializedProperty property)
	{
		return m_Bridge.IsLocalizedString(property, fieldInfo);
	}

	string GetTranslation(SerializedProperty property)
	{
		SerializedProperty termProperty = property.FindPropertyRelative(RelativeTermPropertyPath);
		if (termProperty == null || string.IsNullOrWhiteSpace(termProperty.stringValue))
		{
			return string.Empty;
		}

		string translation;
		return m_Bridge.TryGetTranslation(termProperty.stringValue, out translation)
			? translation
			: string.Empty;
	}

	float GetInvalidPropertyHeight(SerializedProperty property)
	{
		PreviewState state = GetState(property);
		float availableWidth = state.LastKnownWidth > 0f
			? state.LastKnownWidth
			: GetEstimatedInspectorWidth();
		return Mathf.Max(
			EditorGUIUtility.singleLineHeight * 3f,
			EditorStyles.helpBox.CalcHeight(InvalidPropertyContent, availableWidth));
	}

	static void DrawPreview(
		Rect previewRect,
		I2PreviewContentLayout contentLayout,
		PreviewState state,
		GUIStyle style)
	{
		if (!contentLayout.NeedsScrolling)
		{
			state.ScrollPosition = Vector2.zero;
			GUI.Box(previewRect, state.Content, style);
			return;
		}

		float maximumScrollY = Mathf.Max(0f, contentLayout.ContentHeight - previewRect.height);
		state.ScrollPosition.y = Mathf.Clamp(state.ScrollPosition.y, 0f, maximumScrollY);
		state.ScrollPosition.x = 0f;

		Rect contentRect = new Rect(
			0f,
			0f,
			contentLayout.ContentWidth,
			contentLayout.ContentHeight);
		state.ScrollPosition = GUI.BeginScrollView(
			previewRect,
			state.ScrollPosition,
			contentRect,
			false,
			true);
		GUI.Box(contentRect, state.Content, style);
		GUI.EndScrollView();
	}

	static float GetLineHeight(GUIStyle style)
	{
		return style.lineHeight > 0f
			? style.lineHeight
			: EditorGUIUtility.singleLineHeight;
	}

	static float GetVerticalScrollbarWidth()
	{
		GUIStyle scrollbar = GUI.skin == null ? null : GUI.skin.verticalScrollbar;
		if (scrollbar == null)
		{
			return EditorGUIUtility.singleLineHeight;
		}

		float width = scrollbar.fixedWidth;
		if (width <= 0f)
		{
			width = scrollbar.CalcSize(GUIContent.none).x;
		}

		if (width <= 0f)
		{
			width = EditorGUIUtility.singleLineHeight;
		}

		return Mathf.Max(0f, width + scrollbar.margin.left + scrollbar.margin.right);
	}

	static float GetEstimatedInspectorWidth()
	{
		return Mathf.Max(1f, EditorGUIUtility.currentViewWidth - EstimatedInspectorPadding);
	}

	PreviewState GetState(SerializedProperty property)
	{
		string key = GetStateKey(property);
		PreviewState state;
		if (m_States.TryGetValue(key, out state))
		{
			return state;
		}

		if (m_States.Count >= MaximumCachedPropertyCount)
		{
			m_States.Clear();
		}

		state = new PreviewState();
		m_States.Add(key, state);
		return state;
	}

	static string GetStateKey(SerializedProperty property)
	{
		UnityEngine.Object[] targets = property.serializedObject.targetObjects;
		StringBuilder key = new StringBuilder((targets.Length * 12) + property.propertyPath.Length + 1);
		for (int i = 0; i < targets.Length; i++)
		{
			key.Append(targets[i] == null ? 0 : targets[i].GetInstanceID());
			key.Append(',');
		}

		key.Append('|');
		key.Append(property.propertyPath);
		return key.ToString();
	}

	I2PreviewAttribute Attribute
	{
		get { return attribute as I2PreviewAttribute; }
	}

	uint RequestedLineCount
	{
		get { return Attribute == null ? 0 : Attribute.LineHeight; }
	}

	sealed class PreviewState
	{
		internal readonly GUIContent Content = new GUIContent(string.Empty);
		internal Vector2 ScrollPosition;
		internal float DesiredPreviewHeight;
		internal float LastKnownWidth;

		string m_Translation = string.Empty;

		internal void SetTranslation(string translation)
		{
			translation = translation ?? string.Empty;
			if (string.Equals(m_Translation, translation, StringComparison.Ordinal))
			{
				return;
			}

			m_Translation = translation;
			Content.text = translation;
			ScrollPosition = Vector2.zero;
		}
	}
}
