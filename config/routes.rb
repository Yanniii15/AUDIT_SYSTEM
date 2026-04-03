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
  
  get 'manage_staff', to: 'users#index'
  
  # Route to handle adding money to a staff member's wallet
  patch 'users/:id/add_pcf', to: 'users#add_pcf', as: :add_pcf
  patch 'users/:id/assign_manager', to: 'users#assign_manager', as: :assign_manager
  
  # -----------------------------------------------------------------
  # NEW: Route to handle locking in the morning float from the dashboard
  # -----------------------------------------------------------------
  patch 'update_starting_float', to: 'users#update_starting_float', as: :update_starting_float
end