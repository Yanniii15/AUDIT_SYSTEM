class AddPcfBalanceToUsers < ActiveRecord::Migration[8.1]
  def change
    add_column :users, :pcf_balance, :decimal
  end
end
