// https://github.com/ShadowIgnition/I2Loc-LocalizedStringPreview
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(I2PreviewAttribute))]
public class I2PreviewDrawer : PropertyDrawer
{
	const string InvalidPropertyMessage =
		"I2Preview requires an I2.Loc.LocalizedString field with valid serialized data.";
	const string MixedValuesPreview = "— Multiple Values —";
	const string TranslationUnavailablePreview = "Translation unavailable";
	const int MaximumCachedPropertyCount = 512;
	const float EstimatedInspectorPadding = 40f;

	static readonly GUIContent InvalidPropertyContent = new GUIContent(InvalidPropertyMessage);
	static readonly I2LocalizationBridge LocalizationEventBridge = new I2LocalizationBridge();

	readonly I2LocalizationBridge m_Bridge = new I2LocalizationBridge();
	readonly Dictionary<string, PreviewState> m_States =
		new Dictionary<string, PreviewState>();
	static int s_TranslationRevision;

	static I2PreviewDrawer()
	{
		LocalizationEventBridge.TrySubscribeToLocalizationChanges(InvalidateTranslations);
		Undo.undoRedoPerformed += InvalidateTranslations;
		EditorApplication.projectChanged += InvalidateTranslations;
		EditorApplication.playModeStateChanged += delegate { InvalidateTranslations(); };
	}

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

		RefreshPreview(property, state);

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
		RefreshPreview(property, state);

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

	void RefreshPreview(SerializedProperty property, PreviewState state)
	{
		I2LocalizedStringData data;
		bool hasMultipleDifferentValues;
		if (!m_Bridge.TryReadData(property, out data, out hasMultipleDifferentValues))
		{
			state.SetPreview(
				default(I2LocalizedStringData),
				false,
				string.Empty,
				s_TranslationRevision,
				TranslationUnavailablePreview);
			return;
		}

		string language = m_Bridge.CurrentLanguage;
		if (state.IsCurrent(data, hasMultipleDifferentValues, language, s_TranslationRevision))
		{
			return;
		}

		string translation = MixedValuesPreview;
		bool canCacheTranslation = true;
		if (!hasMultipleDifferentValues &&
			!m_Bridge.TryGetTranslation(data, out translation))
		{
			translation = TranslationUnavailablePreview;
			canCacheTranslation = false;
		}

		state.SetPreview(
			data,
			hasMultipleDifferentValues,
			language,
			s_TranslationRevision,
			translation,
			canCacheTranslation);
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

	static void InvalidateTranslations()
	{
		unchecked
		{
			s_TranslationRevision++;
		}
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

		I2LocalizedStringData m_Data;
		bool m_HasCachedTranslation;
		bool m_HasMultipleDifferentValues;
		string m_Language = string.Empty;
		string m_Translation = string.Empty;
		int m_TranslationRevision;

		internal bool IsCurrent(
			I2LocalizedStringData data,
			bool hasMultipleDifferentValues,
			string language,
			int translationRevision)
		{
			return m_HasCachedTranslation &&
				m_Data.Equals(data) &&
				m_HasMultipleDifferentValues == hasMultipleDifferentValues &&
				string.Equals(m_Language, language ?? string.Empty, StringComparison.Ordinal) &&
				m_TranslationRevision == translationRevision;
		}

		internal void SetPreview(
			I2LocalizedStringData data,
			bool hasMultipleDifferentValues,
			string language,
			int translationRevision,
			string translation,
			bool canCacheTranslation = true)
		{
			translation = translation ?? string.Empty;
			bool displayedTextChanged =
				!string.Equals(m_Translation, translation, StringComparison.Ordinal);

			m_Data = data;
			m_HasCachedTranslation = canCacheTranslation;
			m_HasMultipleDifferentValues = hasMultipleDifferentValues;
			m_Language = language ?? string.Empty;
			m_TranslationRevision = translationRevision;
			m_Translation = translation;
			Content.text = translation;

			if (!displayedTextChanged)
			{
				return;
			}

			ScrollPosition = Vector2.zero;
		}
	}
}
