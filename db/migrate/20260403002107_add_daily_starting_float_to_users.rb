class AddDailyStartingFloatToUsers < ActiveRecord::Migration[8.1]
  def change
    add_column :users, :daily_starting_float, :decimal
  end
end
