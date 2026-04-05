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
  # Temporary route to upgrade a specific user to Admin
  get '/upgrade_my_account', to: proc {
    # Replace 'admin@example.com' with the email you used to log in!
    user = User.find_by(email: 'admin@example.com')

    if user
      # 1. Try to set a 'role' column if it exists (covers string or enum)
      if user.respond_to?(:role=)
        user.update(role: 'admin') # Try string
        user.update(role: 0) if user.role != 'admin' # Try integer/enum if string failed
      end

      # 2. Try common boolean columns
      user.update(admin: true) if user.respond_to?(:admin=)
      user.update(is_admin: true) if user.respond_to?(:is_admin=)

      [200, {'Content-Type' => 'text/plain'}, ["Success! User #{user.email} is now an ADMIN. Refresh your dashboard!"]]
    else
      [200, {'Content-Type' => 'text/plain'}, ["User not found. Check the email in routes.rb!"]]
    end
  }
end