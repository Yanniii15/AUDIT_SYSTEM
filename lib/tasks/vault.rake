namespace :vault do
  desc "Wipe all staff wallets and starting floats back to zero"
  task daily_reset: :environment do
    puts "🧹 Starting the Midnight Vault Wipe..."
    
    # Update every single user to have 0 balance and 0 float
    User.update_all(pcf_balance: 0.0, daily_starting_float: 0.0)
    
    puts "✅ All cash boxes have been successfully reset to ₱0.00!"
  end
end