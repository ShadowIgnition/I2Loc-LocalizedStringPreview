using I2.Loc;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(I2PreviewAttribute))]
public class I2PreviewDrawer : LocalizedStringDrawer
{
	/// <summary>
	/// Gets the <see cref="I2PreviewAttribute"/> associated with this drawer.
	/// </summary>
	public I2PreviewAttribute Attribute { get { return attribute as I2PreviewAttribute; } }

	/// <summary>
	/// Draws the GUI elements for the property, then draws the GUI elements for the preview.
	/// </summary>
	/// <param name="rect">The position and size of the GUI element.</param>
	/// <param name="property">The serialized property to draw.</param>
	/// <param name="label">The label of the property.</param>
	public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
	{
		// Override the OnGUI method to draw the GUI elements for the property

		// Set the height to a single line and try to draw the base property
		rect.height = EditorGUIUtility.singleLineHeight;

		// Check if the property type is valid
		if (!IsValidType(property))
		{
			// If the type is invalid, display an error message in a help box
			rect.position = new Vector2(rect.position.x, rect.position.y + EditorGUIUtility.singleLineHeight);
			EditorGUI.HelpBox(rect, property.name + " type not supported, must be " + nameof(LocalizedString), MessageType.Error);
			return;
		}

		// Call the base implementation of OnGUI to draw the default property field
		base.OnGUI(rect, property, label);

		// Get the translation for the property
		string translation = GetTranslation(property);

		// Cache the height of the text area for the translation
		CachePreviewHeight(translation, rect.width);

		// Setup rect for description box
		rect.position = new Vector2(rect.position.x, rect.position.y + rect.height);
		rect.xMax = rect.xMax;
		rect.height = m_Height;

		// Draw the translation in a text area
		EditorGUI.TextArea(rect, translation, EditorStyles.textArea);
	}

	/// <summary>
	/// Gets the height of the property field, uses m_Height to adjust to the calculated size.
	/// </summary>
	/// <param name="property">The serialized property.</param>
	/// <param name="label">The label of the property.</param>
	/// <returns>The height of the property field.</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		// Override the GetPropertyHeight method to specify the height of the property field

		// Calculate the base height using the base implementation
		// Add the height of the translation text area
		return base.GetPropertyHeight(property, label) + m_Height;
	}

	/// <summary>
	/// Checks if the serialized property type is valid.
	/// </summary>
	/// <param name="property">The serialized property.</param>
	/// <returns><c>true</c> if the type is valid; otherwise, <c>false</c>.</returns>
	static bool IsValidType(SerializedProperty property)
	{
		// Check if the property type is valid (LocalizedString)
		return property.type == nameof(LocalizedString);
	}

	/// <summary>
	/// Gets the translation for the serialized property.
	/// </summary>
	/// <param name="property">The serialized property.</param>
	/// <returns>The translation string.</returns>
	string GetTranslation(SerializedProperty property)
	{
		// Get the term property and the translation for the description box
		SerializedProperty termProp = property.FindPropertyRelative(RelativeTermPropertyPath);

		string translation = string.Empty;

		// Skip translation if the term is null or empty
		if (!string.IsNullOrWhiteSpace(termProp.stringValue))
		{
			// Retrieve the translation from the LocalizationManager based on the term
			translation = LocalizationManager.GetTranslation(termProp.stringValue);
		}

		return translation;
	}

	/// <summary>
	/// Caches the height of the translation text area.
	/// </summary>
	/// <param name="translation">The translation string.</param>
	/// <param name="width">The width of the text area.</param>
	void CachePreviewHeight(string translation, float width)
	{
		if (Attribute.LineHeight > 0)
		{
			// If a custom line height is specified, use it
			m_Height = Attribute.LineHeight * GUI.skin.textArea.lineHeight;
		}
		else
		{
			// Calculate the height of the text area
			m_Height = GUI.skin.textArea.CalcHeight(new GUIContent(translation), width);
			// Clamp the height within the specified range (Don't want to be able to make the editor preview too big)
			m_Height = Mathf.Clamp(m_Height, GUI.skin.textArea.lineHeight * AUTO_MIN_HEIGHT, GUI.skin.textArea.lineHeight * AUTO_MAX_HEIGHT);
		}
	}

	// Member variables and constants

	float m_Height;  // Height of the translation text area
	const int AUTO_MAX_HEIGHT = 5;  // Maximum number of lines for the text area
	const int AUTO_MIN_HEIGHT = 2;  // Minimum number of lines for the text area
	const string RelativeTermPropertyPath = "mTerm";  // Relative path to the term property
}