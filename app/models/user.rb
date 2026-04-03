class User < ApplicationRecord
  devise :database_authenticatable, :registerable,
         :recoverable, :rememberable, :validatable

  has_many :expenses 

  # UPDATED: Mapping the enum to strings to match your schema.rb
  enum :role, { staff: "staff", co_admin: "co_admin", admin: "admin" }

  has_many :staff_members, class_name: "User", foreign_key: "manager_id"
  belongs_to :manager, class_name: "User", optional: true
end