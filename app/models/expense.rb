require 'base64'

class Expense < ApplicationRecord
  belongs_to :user
  has_one_attached :receipt_photo

  def analyze_receipt
    return unless receipt_photo.attached?

    # PASTE YOUR KEY DIRECTLY HERE FOR NOW
    api_key = "YOUR_ACTUAL_KEY_FROM_AI_STUDIO"
    client = Gemini::Client.new(api_key: api_key)

    image_data = {
      inline_data: {
        data: Base64.strict_encode64(receipt_photo.download),
        mime_type: receipt_photo.content_type
      }
    }

    # Precise prompt for Gemini 1.5 Flash
    prompt = "Return ONLY a JSON object from this receipt with: { \"total\": number, \"description\": \"string summary\" }"

    begin
      response = client.generate_content([prompt, image_data])
      
      # Extract text and strip markdown if Gemini adds it
      raw_text = response.dig("candidates", 0, "content", "parts", 0, "text")
      clean_json = raw_text.gsub(/```json|```/, "").strip
      
      result = JSON.parse(clean_json)

      # Auto-fill the fields
      self.amount = result["total"]
      self.description = result["description"]
      self.notes = "AI Scan Completed at #{Time.now.strftime('%I:%M %p')}"
    rescue => e
      self.notes = "AI Error: #{e.message}"
    end
  end
end