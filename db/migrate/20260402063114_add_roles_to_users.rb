class AddRolesToUsers < ActiveRecord::Migration[8.1]
  def change
    add_column :users, :manager_id, :integer
  end
end
