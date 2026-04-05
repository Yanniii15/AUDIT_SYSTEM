Rails.application.routes.draw do
  devise_for :users
  
  # 1. Standard CRUD routes for Expenses (Index, New, Create, Edit, etc.)
  # We add a 'collection' block to create a custom URL: /expenses/report
  resources :expenses do
    collection do
      get :report
    end
  end

  # 2. Defines the root path route ("/")
  # This makes it so your Auntie doesn't have to type /expenses in the URL
  root "expenses#index"

  # 3. Health check route (Optional, included by default in Rails 8)
  get "up" => "rails/health#show", as: :rails_health_check

  # Render dynamic PWA files from app/views/pwa/* (Optional, default in Rails 8)
  get "service-worker" => "rails/pwa#service_worker", as: :pwa_service_worker
  get "manifest" => "rails/pwa#manifest", as: :pwa_manifest
  
  # ==========================================
  # USER & STAFF MANAGEMENT ROUTES
  # ==========================================
  get 'manage_staff', to: 'users#index'
  
  # Routes to handle PCF and Managers
  patch 'users/:id/add_pcf', to: 'users#add_pcf', as: :add_pcf
  patch 'users/:id/assign_manager', to: 'users#assign_manager', as: :assign_manager
  
  # NEW: Routes for Admin to Edit and Delete accounts
  resources :users, only: [:edit, :update, :destroy]

end