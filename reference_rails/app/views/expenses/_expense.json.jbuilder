json.extract! expense, :id, :entry_date, :description, :amount, :department, :pcf_source, :is_verified, :created_at, :updated_at
json.url expense_url(expense, format: :json)
