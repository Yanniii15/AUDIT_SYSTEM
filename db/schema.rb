# This file is auto-generated from the current state of the database. Instead
# of editing this file, please use the migrations feature of Active Record to
# incrementally modify your database, and then regenerate this schema definition.
#
# This file is the source Rails uses to define your schema when running `bin/rails
# db:schema:load`. When creating a new database, `bin/rails db:schema:load` tends to
# be faster and is potentially less error prone than running all of your
# migrations from scratch. Old migrations may fail to apply correctly if those
# migrations use external dependencies or application code.
#
# It's strongly recommended that you check this file into your version control system.

ActiveRecord::Schema[8.1].define(version: 2026_04_03_002107) do
  # These are extensions that must be enabled in order to support this database
  enable_extension "pg_catalog.plpgsql"

  create_table "active_storage_attachments", force: :cascade do |t|
    t.bigint "blob_id", null: false
    t.datetime "created_at", null: false
    t.string "name", null: false
    t.bigint "record_id", null: false
    t.string "record_type", null: false
    t.index ["blob_id"], name: "index_active_storage_attachments_on_blob_id"
    t.index ["record_type", "record_id", "name", "blob_id"], name: "index_active_storage_attachments_uniqueness", unique: true
  end

  create_table "active_storage_blobs", force: :cascade do |t|
    t.bigint "byte_size", null: false
    t.string "checksum"
    t.string "content_type"
    t.datetime "created_at", null: false
    t.string "filename", null: false
    t.string "key", null: false
    t.text "metadata"
    t.string "service_name", null: false
    t.index ["key"], name: "index_active_storage_blobs_on_key", unique: true
  end

  create_table "active_storage_variant_records", force: :cascade do |t|
    t.bigint "blob_id", null: false
    t.string "variation_digest", null: false
    t.index ["blob_id", "variation_digest"], name: "index_active_storage_variant_records_uniqueness", unique: true
  end

  create_table "departments", id: :serial, force: :cascade do |t|
    t.string "name", limit: 50, null: false

    t.unique_constraint ["name"], name: "departments_name_key"
  end

  create_table "expenses", id: :serial, force: :cascade do |t|
    t.decimal "amount", precision: 12, scale: 2, null: false
    t.string "department", limit: 50
    t.text "description", null: false
    t.date "entry_date", default: -> { "CURRENT_DATE" }
    t.boolean "is_verified", default: false
    t.text "notes"
    t.string "pcf_source", limit: 50
    t.bigint "user_id"
    t.index ["user_id"], name: "index_expenses_on_user_id"
  end

  create_table "petty_cash_logs", id: :serial, force: :cascade do |t|
    t.decimal "amount", precision: 12, scale: 2, null: false
    t.string "custodian_name", limit: 50, null: false
    t.date "entry_date", default: -> { "CURRENT_DATE" }
    t.text "notes"
  end

  create_table "users", force: :cascade do |t|
    t.datetime "created_at", null: false
    t.decimal "daily_starting_float"
    t.string "email", default: "", null: false
    t.string "encrypted_password", default: "", null: false
    t.integer "manager_id"
    t.string "name"
    t.decimal "pcf_balance"
    t.datetime "remember_created_at"
    t.datetime "reset_password_sent_at"
    t.string "reset_password_token"
    t.string "role", default: "staff"
    t.datetime "updated_at", null: false
    t.index ["email"], name: "index_users_on_email", unique: true
    t.index ["reset_password_token"], name: "index_users_on_reset_password_token", unique: true
  end

  add_foreign_key "active_storage_attachments", "active_storage_blobs", column: "blob_id"
  add_foreign_key "active_storage_variant_records", "active_storage_blobs", column: "blob_id"
  add_foreign_key "expenses", "users"
end
